using System;
using HarmonyLib;
using UnityEngine.Networking;
using static Tank.CollisionInfo;

namespace WaterMod
{
    static class NetworkHandler
    {
        static UnityEngine.Networking.NetworkInstanceId Host;
        static bool HostExists = false;

        private static float serverWaterHeight = -1000f;

        public static float ServerWaterHeight
        {
            get { return serverWaterHeight; }
            set
            {
                serverWaterHeight = value;
                TryBroadcastNewHeight(serverWaterHeight);
            }
        }

        public class WaterChangeMessage : UnityEngine.Networking.MessageBase
        {
            public WaterChangeMessage() { }
            public WaterChangeMessage(float Height)
            {
                this.Height = Height;
            }
            public override void Deserialize(UnityEngine.Networking.NetworkReader reader)
            {
                this.Height = reader.ReadSingle();
            }

            public override void Serialize(UnityEngine.Networking.NetworkWriter writer)
            {
                writer.Write(this.Height);
            }

            public float Height;
        }

        public static void TryBroadcastNewHeight(float Water)
        {
            if (HostExists) try {
                // WaterMod.networkingWrapper.BroadcastToAll(new WaterChangeMessage(Water));
                WaterMod.networkingWrapper.SendMessageToServer(new WaterChangeMessage(Water));
                // Singleton.Manager<ManNetwork>.inst.SendToAllClients(WaterChange, new WaterChangeMessage(Water), Host);
                Console.WriteLine("Sent new water level to host: " + Water);
            }
            catch {
                Console.WriteLine("Failed to send new water level... " + Water);
            }
        }

        public static void OnHeightChanged(WaterChangeMessage obj, UnityEngine.Networking.NetworkMessage netMsg)
        {
            serverWaterHeight = obj.Height;
            Console.WriteLine("Received new water level from host, changing to " + serverWaterHeight.ToString());
        }

        public static void OnClientChangeWaterHeight(WaterChangeMessage obj, UnityEngine.Networking.NetworkMessage netMsg)
        {
            serverWaterHeight = obj.Height;
            Console.WriteLine("Received new water level from client, changing to " + serverWaterHeight.ToString());
            WaterMod.networkingWrapper.BroadcastMessageToAllExceptHost<WaterChangeMessage>(obj);
        }

        public static class Patches
        {
            //[HarmonyPatch(typeof(ManLooseBlocks), "RegisterMessageHandlers")]
            //static class CreateWaterHooks
            //{
            //    static void Postfix
            //}

            [HarmonyPatch(typeof(NetPlayer), "OnRecycle")]
            static class OnRecycle
            {
                static void Postfix(NetPlayer __instance)
                {
                    if (__instance.isServer || __instance.isLocalPlayer)
                    {
                        serverWaterHeight = -1000f;
                        Console.WriteLine("Discarded " + __instance.netId.ToString() + " and reset server water level");
                        HostExists = false;
                    }
                }
            }

            [HarmonyPatch(typeof(NetPlayer), "OnStartClient")]
            static class OnStartClient
            {
                static void Postfix(NetPlayer __instance)
                {
                    // Singleton.Manager<ManNetwork>.inst.SubscribeToClientMessage(__instance.netId, WaterChange, new ManNetwork.MessageHandler(OnClientChangeWaterHeight));
                    Console.WriteLine("Subscribed " + __instance.netId.ToString() + " to water level updates from host. Sending current level");
                    TryBroadcastNewHeight(serverWaterHeight);
                }
            }

            [HarmonyPatch(typeof(NetPlayer), "OnStartServer")]
            static class OnStartServer
            {
                static void Postfix(NetPlayer __instance)
                {
                    if (!HostExists)
                    {
                        serverWaterHeight = -1000f;
                        //Singleton.Manager<ManNetwork>.inst.SubscribeToServerMessage(__instance.netId, WaterChange, new ManNetwork.MessageHandler(OnServerChangeWaterHeight));
                        Console.WriteLine("Host started, hooked water level broadcasting to " + __instance.netId.ToString());
                        Host = __instance.netId;
                        HostExists = true;
                    }
                }
            }
        }
    }
}
