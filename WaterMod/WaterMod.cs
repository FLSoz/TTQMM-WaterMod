using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using TTCustomNetworkingWrapper;
using UnityEngine;
using static WaterMod.NetworkHandler;

namespace WaterMod
{
    public class WaterMod : ModBase
    {
        internal static bool Inited = false;
        internal static bool TTMMInited = false;

        internal static Logger logger;
        internal static void ConfigureLogger()
        {
            logger = new Logger("WaterMod");
            logger.Info("Logger is setup");
        }

        public override bool HasEarlyInit()
        {
            return true;
        }

        public static Type[] LoadBefore()
        {
            return new Type[] { typeof(TechComponentInjector.TechComponentInjector) };
        }

        internal static CustomNetworkingWrapper<WaterChangeMessage> networkingWrapper;

        public void ManagedEarlyInit()
        {
            // Networking
            CustomNetworkingWrapper<WaterChangeMessage> wrapper = ManCustomNetHandler.GetNetworkingWrapper<WaterChangeMessage>(
                "WaterMod",
                NetworkHandler.OnHeightChanged,
                NetworkHandler.OnClientChangeWaterHeight
            );
            ManCustomNetHandler.RegisterNetworkingWrapper(wrapper);
            networkingWrapper = wrapper;

            if (!Inited)
            {
                ConfigureLogger();
                QPatch.SetupResources();
                QPatch.ApplyPatch();
                Inited = true;
            }
        }

        public override void EarlyInit()
        {
            this.ManagedEarlyInit();
        }

        private static FieldInfo allBlocks = AccessTools.Field(typeof(ManSpawn), "m_BlockPrefabs");
        private static MethodInfo LookupPool = AccessTools.Method(typeof(ComponentPool), "LookupPool");
        Dictionary<int, Transform> vanillaBlocks = (Dictionary<int, Transform>)allBlocks.GetValue(Singleton.Manager<ManSpawn>.inst);

        // We remove WaterBlock, etc. from all blocks
        public override void DeInit()
        {
            TechComponentInjector.TechComponentInjector.RemoveTechComponentToInject(typeof(WaterBuoyancy.WaterTank));

            // runs after all modded blocks have been removed. Remove added WaterBlock
            ComponentPool poolManager = Singleton.Manager<ComponentPool>.inst;
            WaterBuoyancy.WaterBlock component;
            foreach (KeyValuePair<int, Transform> block in vanillaBlocks)
            {
                Transform blockPrefab = block.Value;
                ComponentPool.Pool pool = (ComponentPool.Pool)LookupPool.Invoke(poolManager, new object[] { blockPrefab });
                foreach (ComponentPool.Pool.Poolable poolable in pool.freeList)
                {
                    Transform pooledBlockTransform = poolable.component as Transform;
                    component = pooledBlockTransform.gameObject.GetComponent<WaterBuoyancy.WaterBlock>();
                    UnityEngine.Object.Destroy(component);
                }
                component = pool.template.gameObject.GetComponent<WaterBuoyancy.WaterBlock>();
                UnityEngine.Object.Destroy(component);
            }
        }

        // We add WaterBlock, etc. to all blocks
        public override void Init()
        {
            TechComponentInjector.TechComponentInjector.AddTechComponentToInject(typeof(WaterBuoyancy.WaterTank));

            ComponentPool poolManager = Singleton.Manager<ComponentPool>.inst;
            foreach (KeyValuePair<int, Transform> block in vanillaBlocks)
            {
                Transform blockPrefab = block.Value;
                ComponentPool.Pool pool = (ComponentPool.Pool)LookupPool.Invoke(poolManager, new object[]{ blockPrefab });
                foreach (ComponentPool.Pool.Poolable poolable in pool.freeList)
                {
                    Transform pooledBlockTransform = poolable.component as Transform;
                    pooledBlockTransform.gameObject.AddComponent<WaterBuoyancy.WaterBlock>();
                }
                // we touch template so new blocks get this auto-added
                pool.template.gameObject.AddComponent<WaterBuoyancy.WaterBlock>();
                // we don't touch originalPrefab, since that's only used by GetOrignialPrefab, which is only used for stuff that isn't blocks
            }
        }
    }
}
