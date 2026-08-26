using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Colyseus;
using Colyseus.Schema;
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
        [SerializeField] private string endpoint = "ws://localhost:2567";
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

        private sealed class RemotePlayerView
        {
            public RemotePlayerView(GameObject marker, Vector3 position, float yaw)
            {
                Marker = marker;
                TargetPosition = position;
                TargetYaw = yaw;
            }

            public GameObject Marker { get; }
            public Vector3 TargetPosition { get; set; }
            public float TargetYaw { get; set; }
        }

        public void SetLocalPlayerMarker(Transform marker)
        {
            localPlayerMarker = marker;
        }

        private async void Start()
        {
            await Connect();
        }

        private async Task Connect()
        {
            status = "Connecting...";
            lastError = string.Empty;

            try
            {
                client = new Client(endpoint);
                room = await client.JoinOrCreate<DynamicSchema>(roomName);
                room.OnLeave += code => status = $"Disconnected ({code})";
                room.OnError += (code, message) =>
                {
                    lastError = $"Room error {code}: {message}";
                    status = "Error";
                };

                var callbacks = Callbacks.Get(room);
                callbacks.OnAdd<DynamicSchema>("players", (key, player) =>
                {
                    AddOrUpdatePlayer(key, player);
                    callbacks.OnChange(player, () => AddOrUpdatePlayer(key, player));
                });
                callbacks.OnRemove<DynamicSchema>("players", (key, player) => RemovePlayer(key));

                await room.WaitForFirstState();
                status = "Connected";
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                status = "Connection failed";
                Debug.LogException(exception, this);
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

            if (!playerMarkers.TryGetValue(key, out var remotePlayer))
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                marker.name = $"NetworkPlayer_{key}";
                marker.transform.SetParent(remotePlayerRoot != null ? remotePlayerRoot : transform, true);
                marker.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
                marker.GetComponent<Renderer>().material.color = ColorForKey(key);
                marker.GetComponent<Collider>().enabled = false;
                marker.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                remotePlayer = new RemotePlayerView(marker, position, yaw);
                playerMarkers.Add(key, remotePlayer);
            }

            remotePlayer.TargetPosition = position;
            remotePlayer.TargetYaw = yaw;
        }

        private void RemovePlayer(string key)
        {
            if (playerMarkers.TryGetValue(key, out var remotePlayer))
            {
                Destroy(remotePlayer.Marker);
                playerMarkers.Remove(key);
            }
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

        private static Color ColorForKey(string key)
        {
            var hash = Math.Abs(key.GetHashCode());
            return Color.HSVToRGB((hash % 360) / 360f, 0.7f, 0.95f);
        }

        private async void OnDestroy()
        {
            if (room != null)
            {
                await room.Leave();
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12f, 12f, 420f, 90f), GUI.skin.box);
            GUILayout.Label($"Colyseus: {status}");
            GUILayout.Label($"Room: {roomName}   Player: {(localPlayerNumber == 0 ? "-" : localPlayerNumber.ToString())}");
            GUILayout.Label($"Transform sync: {(room != null && status == "Connected" ? $"{transformSendRate:0} Hz" : "offline")}");
            if (!string.IsNullOrEmpty(lastError))
            {
                GUILayout.Label(lastError);
            }
            GUILayout.EndArea();
        }
    }
}
