using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonsNShops
{
    // Only the authority steps this scene. Clients render the resulting crate poses.
    internal sealed class CratePhysics : IDisposable
    {
        private readonly Scene scene;
        private readonly PhysicsScene physics;
        private readonly Dictionary<int, Rigidbody> bodies = new();
        private readonly List<int> removed = new();

        public CratePhysics()
        {
            scene = SceneManager.CreateScene("Shop cargo " + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physics = scene.GetPhysicsScene();
            Box("Floor", Vector3.down * 0.2f, new Vector3(20, 0.4f, 20));
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Side wall", Vector3.right * (side * 10), new Vector3(0.4f, 12, 20));
                Box("End wall", Vector3.forward * (side * 10), new Vector3(20, 12, 0.4f));
            }
            foreach (Rect rect in Map.ShopObstacles)
            {
                float height = rect.height > 2 ? 1.6f : 0.96f;
                Box("Furniture", new Vector3(rect.center.x, height / 2, rect.center.y),
                    new Vector3(rect.width, height, rect.height));
            }
            for (int side = 0; side < 2; side++)
                foreach (float height in new[] { 0.925f, 1.47f })
                    Box("Rack", ShopTargets.RackPosition(side, height), new Vector3(0.65f, 0.065f, 6.1f));
            Bounds bin = ShopTargets.Station(Site.Bin);
            Box("Bin", bin.center, bin.size);
        }

        private GameObject Box(string name, Vector3 position, Vector3 size)
        {
            var obj = new GameObject(name);
            SceneManager.MoveGameObjectToScene(obj, scene);
            obj.transform.position = position;
            obj.AddComponent<BoxCollider>().size = size;
            return obj;
        }

        public void Release(int id)
        {
            if (!bodies.Remove(id, out var body)) return;
            body.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(body.gameObject);
        }

        public bool Tick(List<Crate> crates, float delta)
        {
            removed.Clear();
            foreach (var entry in bodies)
                if (!crates.Exists(c => c.id == entry.Key)) removed.Add(entry.Key);
            foreach (int id in removed) Release(id);
            if (crates.Count == 0) return false;
            foreach (var crate in crates)
            {
                if (bodies.ContainsKey(crate.id)) continue;
                GameObject obj = Box("Cargo", crate.position, ShopTargets.CrateSize);
                obj.transform.rotation = Quaternion.Euler(crate.angles);
                var body = obj.AddComponent<Rigidbody>();
                body.mass = 1.4f + crate.count * 0.28f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.linearVelocity = crate.velocity;
                body.angularVelocity = crate.spin;
                bodies.Add(crate.id, body);
            }

            physics.Simulate(delta);
            bool moved = false;
            foreach (var crate in crates)
            {
                Rigidbody body = bodies[crate.id];
                moved |= crate.position != body.position || crate.velocity != body.linearVelocity || crate.spin != body.angularVelocity;
                crate.position = body.position;
                crate.angles = body.rotation.eulerAngles;
                crate.velocity = body.linearVelocity;
                crate.spin = body.angularVelocity;
            }
            return moved;
        }

        public void Dispose()
        {
            if (scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
        }
    }
}
