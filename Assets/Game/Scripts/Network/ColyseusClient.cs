using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Colyseus;
using Colyseus.Schema;
using SpiderSwing.Gameplay;
using UnityEngine;

namespace SpiderSwing.Network
{
    /// <summary>
    /// Small connection spike for Milestone 0. It joins the room and renders
    /// the server's player map as colored primitive markers. Gameplay movement
    /// will replace the marker update path in the next milestone.
    /// </summary>
    public sealed class ColyseusClient : MonoBehaviour
    {
        [SerializeField] private string endpoint = "wss://variations-absorption-ent-procedure.trycloudflare.com";
        [SerializeField] private string roomName = "spider_room";
        [SerializeField] private Transform localPlayerMarker;
        [SerializeField] private Transform remotePlayerRoot;
        [SerializeField, Min(1f)] private float transformSendRate = 15f;
        [SerializeField, Min(1f)] private float remoteInterpolationSharpness = 14f;

        private readonly Dictionary<string, RemotePlayerView> playerMarkers = new Dictionary<string, RemotePlayerView>();
        private Client client;
        private Room<DynamicSchema> room;
        private string status = "Disconnected";
        private string lastError = string.Empty;
        private int localPlayerNumber;
        private float nextTransformSendTime;
        private bool isSendingTransform;
        private bool isSendingSkin;
        private bool isSendingAnimation;
        private bool isConnecting;
        private bool isDestroying;
        private CancellationTokenSource connectionCancellation;
        private PlayerUpgradeState localUpgradeState;
        private PlayerSkinVisual localSkinVisual;
        private LocalPlayerController localPlayerController;
        private PlayerAnimationController localAnimationController;
        private readonly Dictionary<string, SkinMaterials> skinCatalog = new Dictionary<string, SkinMaterials>();

        private const string DefaultSkinId = "Default";
        private const float MaxConnectionDurationSeconds = 90f;

        private readonly struct SkinMaterials
        {
            public SkinMaterials(Material arm, Material body)
            {
                Arm = arm;
                Body = body;
            }

            public Material Arm { get; }
            public Material Body { get; }
        }

        private sealed class RemotePlayerView
        {
            public RemotePlayerView(
                GameObject marker,
                Transform visualRoot,
                PlayerSwingVisual swingVisual,
                PlayerAnimationController animationController,
                Vector3 position,
                float yaw)
            {
                Marker = marker;
                VisualRoot = visualRoot;
                SwingVisual = swingVisual;
                AnimationController = animationController;
                TargetPosition = position;
                TargetYaw = yaw;
            }

            public GameObject Marker { get; }
            public Transform VisualRoot { get; }
            public PlayerSwingVisual SwingVisual { get; }
            public PlayerAnimationController AnimationController { get; }
            public Vector3 TargetPosition { get; set; }
            public float TargetYaw { get; set; }
            public bool IsSwinging { get; set; }
            public Vector3 SwingAnchor { get; set; }
        }

        public void SetLocalPlayerMarker(Transform marker)
        {
            localPlayerMarker = marker;
        }

        private void Start()
        {
            ResolveLocalSkinState();
            BuildSkinCatalog();
            BeginConnect();
        }

        private void BeginConnect()
        {
            if (isDestroying || isConnecting)
            {
                return;
            }

            connectionCancellation?.Cancel();
            connectionCancellation?.Dispose();
            connectionCancellation = new CancellationTokenSource();
            _ = ConnectWithRetry(connectionCancellation.Token);
        }

        private async Task ConnectWithRetry(CancellationToken cancellationToken)
        {
            if (isConnecting)
            {
                return;
            }

            isConnecting = true;
            lastError = string.Empty;
            var startedAt = Time.realtimeSinceStartup;
            var attempt = 0;

            try
            {
                while (!cancellationToken.IsCancellationRequested
                       && !isDestroying
                       && Time.realtimeSinceStartup - startedAt < MaxConnectionDurationSeconds)
                {
                    attempt++;
                    status = attempt == 1
                        ? "Connecting..."
                        : $"Waking multiplayer server... (attempt {attempt})";

                    try
                    {
                        await ConnectOnce(cancellationToken);
                        return;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        lastError = exception.Message;
                        Debug.LogWarning($"Multiplayer connection attempt {attempt} failed: {exception.Message}", this);
                        await LeaveCurrentRoom();
                    }

                    var elapsed = Time.realtimeSinceStartup - startedAt;
                    var remaining = MaxConnectionDurationSeconds - elapsed;
                    if (remaining <= 0f)
                    {
                        break;
                    }

                    var delay = Mathf.Min(GetRetryDelaySeconds(attempt), remaining);
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                }

                if (!cancellationToken.IsCancellationRequested && !isDestroying)
                {
                    status = "Connection failed";
                    if (string.IsNullOrEmpty(lastError))
                    {
                        lastError = "The multiplayer server did not become available.";
                    }
                }
            }
            finally
            {
                isConnecting = false;
            }
        }

        private async Task ConnectOnce(CancellationToken cancellationToken)
        {
            client = new Client(ResolveEndpoint());
            var joinedRoom = await client.JoinOrCreate<DynamicSchema>(roomName);
            if (cancellationToken.IsCancellationRequested || isDestroying)
            {
                await joinedRoom.Leave();
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            room = joinedRoom;
            joinedRoom.OnLeave += code =>
            {
                if (room != joinedRoom)
                {
                    return;
                }

                room = null;
                ClearRemotePlayers();
                status = $"Disconnected ({code})";
                if (!isDestroying)
                {
                    BeginConnect();
                }
            };
            joinedRoom.OnError += (code, message) =>
            {
                if (room != joinedRoom)
                {
                    return;
                }

                lastError = $"Room error {code}: {message}";
                status = "Error";
            };

            var callbacks = Callbacks.Get(joinedRoom);
            callbacks.OnAdd<DynamicSchema>("players", (key, player) =>
            {
                AddOrUpdatePlayer(key, player);
                callbacks.OnChange(player, () => AddOrUpdatePlayer(key, player));
            });
            callbacks.OnRemove<DynamicSchema>("players", (key, player) => RemovePlayer(key));

            await joinedRoom.WaitForFirstState();
            cancellationToken.ThrowIfCancellationRequested();
            status = "Connected";
            await SendLocalSkin(localUpgradeState != null ? localUpgradeState.CurrentSkinId : DefaultSkinId);
            if (localAnimationController != null)
            {
                await SendLocalAnimation(localAnimationController.CurrentState);
            }

            if (localPlayerController != null)
            {
                await SendLocalSwing(localPlayerController.IsSwinging, localPlayerController.SwingAnchor);
            }
        }

        private string ResolveEndpoint()
        {
            if (Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var pageUri))
            {
                const string queryKey = "server=";
                var query = pageUri.Query;
                var keyIndex = query.IndexOf(queryKey, StringComparison.OrdinalIgnoreCase);
                if (keyIndex >= 0)
                {
                    var valueStart = keyIndex + queryKey.Length;
                    var valueEnd = query.IndexOf('&', valueStart);
                    if (valueEnd < 0)
                    {
                        valueEnd = query.Length;
                    }

                    var candidate = Uri.UnescapeDataString(query.Substring(valueStart, valueEnd - valueStart));
                    if (candidate.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)
                        || candidate.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }

            return endpoint;
        }

        private static float GetRetryDelaySeconds(int attempt)
        {
            switch (attempt)
            {
                case 1:
                    return 2f;
                case 2:
                    return 4f;
                case 3:
                    return 8f;
                default:
                    return 10f;
            }
        }

        private async Task LeaveCurrentRoom()
        {
            var roomToLeave = room;
            room = null;
            ClearRemotePlayers();
            if (roomToLeave == null)
            {
                return;
            }

            try
            {
                await roomToLeave.Leave();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Unable to leave the multiplayer room cleanly: {exception.Message}", this);
            }
        }

        private void AddOrUpdatePlayer(string key, DynamicSchema player)
        {
            if (player == null)
            {
                return;
            }

            var number = player.Get<int>("playerNumber");
            var isLocalPlayer = room != null && key == room.SessionId;
            if (isLocalPlayer)
            {
                localPlayerNumber = number;
                return;
            }

            var position = new Vector3(
                player.Get<float>("x"),
                player.Get<float>("y"),
                player.Get<float>("z"));
            var yaw = player.Get<float>("yaw");
            var skinId = player.Get<string>("skinId") ?? DefaultSkinId;
            var isSwinging = player.Get<bool>("isSwinging");
            var animationState = ReadAnimationState(player);
            var swingAnchor = new Vector3(
                player.Get<float>("swingAnchorX"),
                player.Get<float>("swingAnchorY"),
                player.Get<float>("swingAnchorZ"));

            if (!playerMarkers.TryGetValue(key, out var remotePlayer))
            {
                var marker = CreateRemoteMarker(
                    key,
                    position,
                    yaw,
                    out var visualRoot,
                    out var swingVisual,
                    out var animationController);
                marker.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                remotePlayer = new RemotePlayerView(
                    marker,
                    visualRoot,
                    swingVisual,
                    animationController,
                    position,
                    yaw);
                playerMarkers.Add(key, remotePlayer);
            }

            remotePlayer.TargetPosition = position;
            remotePlayer.TargetYaw = yaw;
            remotePlayer.IsSwinging = isSwinging;
            remotePlayer.SwingAnchor = swingAnchor;
            remotePlayer.SwingVisual?.SetSwingState(isSwinging, swingAnchor);
            remotePlayer.AnimationController?.SetState(animationState);
            ApplyRemoteSkin(remotePlayer, skinId);
        }

        private GameObject CreateRemoteMarker(
            string key,
            Vector3 position,
            float yaw,
            out Transform visualRoot,
            out PlayerSwingVisual swingVisual,
            out PlayerAnimationController animationController)
        {
            var marker = new GameObject($"NetworkPlayer_{key}");
            marker.transform.SetParent(remotePlayerRoot != null ? remotePlayerRoot : transform, true);
            visualRoot = null;
            swingVisual = null;
            animationController = null;

            if (localSkinVisual != null && localSkinVisual.ModelRoot != null)
            {
                var visual = Instantiate(localSkinVisual.ModelRoot.gameObject, marker.transform, false);
                visual.name = "Visual";
                visual.transform.localPosition = localSkinVisual.ModelRoot.localPosition;
                visual.transform.localRotation = localSkinVisual.ModelRoot.localRotation;
                visual.transform.localScale = localSkinVisual.ModelRoot.localScale;
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                }

                visualRoot = visual.transform;
                swingVisual = marker.AddComponent<PlayerSwingVisual>();
                var line = marker.AddComponent<LineRenderer>();
                swingVisual.Configure(visualRoot, configuredWebLine: line);
                animationController = marker.AddComponent<PlayerAnimationController>();
                animationController.Configure(visualRoot);
                return marker;
            }

            var fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallback.transform.SetParent(marker.transform, false);
            fallback.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
            fallback.GetComponent<Renderer>().material.color = ColorForKey(key);
            fallback.GetComponent<Collider>().enabled = false;
            visualRoot = fallback.transform;
            swingVisual = marker.AddComponent<PlayerSwingVisual>();
            var fallbackLine = marker.AddComponent<LineRenderer>();
            swingVisual.Configure(visualRoot, configuredWebLine: fallbackLine);
            animationController = marker.AddComponent<PlayerAnimationController>();
            animationController.Configure(visualRoot);
            return marker;
        }

        private void ApplyRemoteSkin(RemotePlayerView remotePlayer, string skinId)
        {
            if (remotePlayer == null || remotePlayer.VisualRoot == null)
            {
                return;
            }

            if (!skinCatalog.TryGetValue(skinId, out var materials)
                && !skinCatalog.TryGetValue(DefaultSkinId, out materials))
            {
                return;
            }

            PlayerSkinVisual.ApplyToHierarchy(remotePlayer.VisualRoot, materials.Arm, materials.Body);
        }

        private void RemovePlayer(string key)
        {
            if (playerMarkers.TryGetValue(key, out var remotePlayer))
            {
                Destroy(remotePlayer.Marker);
                playerMarkers.Remove(key);
            }
        }

        private void ClearRemotePlayers()
        {
            foreach (var remotePlayer in playerMarkers.Values)
            {
                if (remotePlayer.Marker != null)
                {
                    Destroy(remotePlayer.Marker);
                }
            }

            playerMarkers.Clear();
            localPlayerNumber = 0;
        }

        private void Update()
        {
            UpdateRemotePlayers();

            if (room == null || localPlayerMarker == null || status != "Connected" || isSendingTransform)
            {
                return;
            }

            if (Time.unscaledTime < nextTransformSendTime)
            {
                return;
            }

            nextTransformSendTime = Time.unscaledTime + 1f / transformSendRate;
            _ = SendLocalTransform();
        }

        private void UpdateRemotePlayers()
        {
            var interpolation = 1f - Mathf.Exp(-remoteInterpolationSharpness * Time.deltaTime);
            foreach (var remotePlayer in playerMarkers.Values)
            {
                if (remotePlayer.Marker == null)
                {
                    continue;
                }

                remotePlayer.Marker.transform.position = Vector3.Lerp(
                    remotePlayer.Marker.transform.position,
                    remotePlayer.TargetPosition,
                    interpolation);
                remotePlayer.Marker.transform.rotation = Quaternion.Slerp(
                    remotePlayer.Marker.transform.rotation,
                    Quaternion.Euler(0f, remotePlayer.TargetYaw, 0f),
                    interpolation);
            }
        }

        private async Task SendLocalTransform()
        {
            isSendingTransform = true;
            var roomAtSend = room;

            try
            {
                var position = localPlayerMarker.position;
                await roomAtSend.Send("transform", new Dictionary<string, object>
                {
                    ["x"] = position.x,
                    ["y"] = position.y,
                    ["z"] = position.z,
                    ["yaw"] = localPlayerMarker.eulerAngles.y,
                });
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                Debug.LogWarning("Unable to send the local player transform.", this);
            }
            finally
            {
                isSendingTransform = false;
            }
        }

        private async Task SendLocalSkin(string skinId)
        {
            if (room == null || status != "Connected" || isSendingSkin)
            {
                return;
            }

            isSendingSkin = true;
            try
            {
                await room.Send("skin", new Dictionary<string, object>
                {
                    ["skinId"] = string.IsNullOrWhiteSpace(skinId) ? DefaultSkinId : skinId,
                });
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                Debug.LogWarning("Unable to send the local player skin.", this);
            }
            finally
            {
                isSendingSkin = false;
            }
        }

        private void ResolveLocalSkinState()
        {
            if (localPlayerMarker == null)
            {
                return;
            }

            var upgradeState = localPlayerMarker.GetComponent<PlayerUpgradeState>();
            if (upgradeState != localUpgradeState)
            {
                if (localUpgradeState != null)
                {
                    localUpgradeState.OnSkinChanged -= HandleLocalSkinChanged;
                }

                localUpgradeState = upgradeState;
                if (localUpgradeState != null)
                {
                    localUpgradeState.OnSkinChanged += HandleLocalSkinChanged;
                }
            }

            var playerController = localPlayerMarker.GetComponent<LocalPlayerController>();
            if (playerController != localPlayerController)
            {
                if (localPlayerController != null)
                {
                    localPlayerController.OnSwingStateChanged -= HandleLocalSwingChanged;
                }

                localPlayerController = playerController;
                if (localPlayerController != null)
                {
                    localPlayerController.OnSwingStateChanged += HandleLocalSwingChanged;
                }
            }

            var animationController = localPlayerMarker.GetComponent<PlayerAnimationController>();
            if (animationController != localAnimationController)
            {
                if (localAnimationController != null)
                {
                    localAnimationController.OnAnimationStateChanged -= HandleLocalAnimationChanged;
                }

                localAnimationController = animationController;
                if (localAnimationController != null)
                {
                    localAnimationController.OnAnimationStateChanged += HandleLocalAnimationChanged;
                }
            }

            localSkinVisual = localPlayerMarker.GetComponent<PlayerSkinVisual>()
                ?? localPlayerMarker.gameObject.AddComponent<PlayerSkinVisual>();
        }

        private void BuildSkinCatalog()
        {
            skinCatalog.Clear();
            if (localSkinVisual != null
                && localSkinVisual.TryGetCurrentMaterials(out var defaultArm, out var defaultBody))
            {
                skinCatalog[DefaultSkinId] = new SkinMaterials(defaultArm, defaultBody);
            }

            foreach (var pad in FindObjectsByType<UpgradePad>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!string.IsNullOrWhiteSpace(pad.UpgradeId)
                    && pad.TryGetSkinMaterials(out var arm, out var body))
                {
                    skinCatalog[pad.UpgradeId] = new SkinMaterials(arm, body);
                }
            }
        }

        private void HandleLocalSkinChanged(string skinId)
        {
            _ = SendLocalSkin(skinId);
        }

        private void HandleLocalSwingChanged(bool active, Vector3 anchor)
        {
            _ = SendLocalSwing(active, anchor);
        }

        private void HandleLocalAnimationChanged(PlayerAnimationState state)
        {
            _ = SendLocalAnimation(state);
        }

        private async Task SendLocalSwing(bool active, Vector3 anchor)
        {
            if (room == null || status != "Connected")
            {
                return;
            }

            try
            {
                await room.Send("swing", new Dictionary<string, object>
                {
                    ["active"] = active,
                    ["anchorX"] = anchor.x,
                    ["anchorY"] = anchor.y,
                    ["anchorZ"] = anchor.z,
                });
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                Debug.LogWarning("Unable to send the local swing state.", this);
            }
        }

        private async Task SendLocalAnimation(PlayerAnimationState state)
        {
            if (room == null
                || status != "Connected"
                || isSendingAnimation
                || !PlayerAnimationController.IsValidState((int)state))
            {
                return;
            }

            isSendingAnimation = true;
            try
            {
                await room.Send("animation", new Dictionary<string, object>
                {
                    ["state"] = (int)state,
                });
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                Debug.LogWarning("Unable to send the local animation state.", this);
            }
            finally
            {
                isSendingAnimation = false;
            }
        }

        private static PlayerAnimationState ReadAnimationState(DynamicSchema player)
        {
            try
            {
                var value = player.Get<int>("animationState");
                return PlayerAnimationController.IsValidState(value)
                    ? (PlayerAnimationState)value
                    : PlayerAnimationState.Idle;
            }
            catch
            {
                // Keep the client compatible with a room that has not yet
                // reloaded the new schema during local development.
                return PlayerAnimationState.Idle;
            }
        }

        private static Color ColorForKey(string key)
        {
            var hash = Math.Abs(key.GetHashCode());
            return Color.HSVToRGB((hash % 360) / 360f, 0.7f, 0.95f);
        }

        private async void OnDestroy()
        {
            isDestroying = true;
            connectionCancellation?.Cancel();

            if (localUpgradeState != null)
            {
                localUpgradeState.OnSkinChanged -= HandleLocalSkinChanged;
            }

            if (localPlayerController != null)
            {
                localPlayerController.OnSwingStateChanged -= HandleLocalSwingChanged;
            }

            if (localAnimationController != null)
            {
                localAnimationController.OnAnimationStateChanged -= HandleLocalAnimationChanged;
            }

            await LeaveCurrentRoom();
            connectionCancellation?.Dispose();
            connectionCancellation = null;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12f, 12f, 420f, 140f), GUI.skin.box);
            GUILayout.Label($"Colyseus: {status}");
            GUILayout.Label($"Room: {roomName}   Player: {(localPlayerNumber == 0 ? "-" : localPlayerNumber.ToString())}");
            GUILayout.Label($"Transform sync: {(room != null && status == "Connected" ? $"{transformSendRate:0} Hz" : "offline")}");
            if (!string.IsNullOrEmpty(lastError))
            {
                GUILayout.Label(lastError);
            }

            if (!isConnecting && status != "Connected" && GUILayout.Button("Retry Multiplayer"))
            {
                BeginConnect();
            }

            GUILayout.EndArea();
        }
    }
}
