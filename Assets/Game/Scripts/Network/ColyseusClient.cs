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

        private readonly Dictionary<string, GameObject> playerMarkers = new Dictionary<string, GameObject>();
        private Client client;
        private Room<DynamicSchema> room;
        private string status = "Disconnected";
        private string lastError = string.Empty;
        private int localPlayerNumber;

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

            if (!playerMarkers.TryGetValue(key, out var marker))
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                marker.name = $"NetworkPlayer_{key}";
                marker.transform.SetParent(remotePlayerRoot != null ? remotePlayerRoot : transform, true);
                marker.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
                marker.GetComponent<Renderer>().material.color = ColorForKey(key);
                playerMarkers.Add(key, marker);
            }

            var position = new Vector3(
                player.Get<float>("x"),
                player.Get<float>("y"),
                player.Get<float>("z"));
            marker.transform.position = position;
            marker.transform.rotation = Quaternion.Euler(0f, player.Get<float>("yaw"), 0f);

            var number = player.Get<int>("playerNumber");
            if (room != null && key == room.SessionId)
            {
                localPlayerNumber = number;
                if (localPlayerMarker != null)
                {
                    localPlayerMarker.position = position;
                    localPlayerMarker.rotation = marker.transform.rotation;
                }
            }
        }

        private void RemovePlayer(string key)
        {
            if (playerMarkers.TryGetValue(key, out var marker))
            {
                Destroy(marker);
                playerMarkers.Remove(key);
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
            if (!string.IsNullOrEmpty(lastError))
            {
                GUILayout.Label(lastError);
            }
            GUILayout.EndArea();
        }
    }
}
