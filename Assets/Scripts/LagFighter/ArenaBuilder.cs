using UnityEngine;

namespace LagFighter
{
    // Escenario 2D de vista lateral, estilo Footsies: una línea de piso,
    // marcas de límites y centro, cámara fija de costado.
    public static class ArenaBuilder
    {
        public static void Build()
        {
            var root = new GameObject("LagFighter.Arena");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform);
            ground.transform.localScale = new Vector3(1.4f, 1f, 0.8f);
            Tint(ground, new Color(0.10f, 0.11f, 0.14f));

            // franja de juego y marcas
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "Strip";
            strip.transform.SetParent(root.transform);
            strip.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            strip.transform.localScale = new Vector3(SimConfig.StageHalfWidth * 2f + 0.9f, 0.02f, 1.6f);
            Tint(strip, new Color(0.16f, 0.17f, 0.21f));

            var center = GameObject.CreatePrimitive(PrimitiveType.Cube);
            center.name = "CenterLine";
            center.transform.SetParent(root.transform);
            center.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            center.transform.localScale = new Vector3(0.05f, 0.02f, 1.6f);
            Tint(center, new Color(0.35f, 0.37f, 0.44f));

            for (int s = -1; s <= 1; s += 2)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Wall" + s;
                wall.transform.SetParent(root.transform);
                wall.transform.localPosition = new Vector3(s * (SimConfig.StageHalfWidth + 0.5f), 0.9f, 0f);
                wall.transform.localScale = new Vector3(0.15f, 1.8f, 1.2f);
                Tint(wall, new Color(0.35f, 0.16f, 0.16f));
            }

            // fondo para contraste
            var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "Backdrop";
            back.transform.SetParent(root.transform);
            back.transform.localPosition = new Vector3(0f, 2.2f, 3.2f);
            back.transform.localScale = new Vector3(16f, 4.4f, 0.1f);
            Tint(back, new Color(0.07f, 0.08f, 0.11f));

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.transform.position = new Vector3(0f, 1.45f, -7.2f);
            cam.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
            cam.fieldOfView = 45f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);

            if (Object.FindAnyObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                lightGo.transform.rotation = Quaternion.Euler(45f, -25f, 0f);
                light.intensity = 1.1f;
            }
        }

        public static void Tint(GameObject go, Color c)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.material.color = c;
        }
    }
}
