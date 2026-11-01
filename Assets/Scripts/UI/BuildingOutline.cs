using System.Collections.Generic;
using UnityEngine;

namespace Evergreen.UI
{
    /// <summary>
    /// Draws a solid-colour outline around a building. Added at runtime by BuildingClickSelector.
    /// For every MeshRenderer in the building it spawns a hidden "hull" child that draws a copy of
    /// the mesh twice with the Evergreen/BuildingOutline shader: a stencil mask pass, then the outline
    /// pass outside that mask. Toggling the hulls shows or hides the outline.
    /// The copy carries smoothed normals in UV3 so the outline has no gaps on hard-edged meshes.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildingOutline : MonoBehaviour
    {
        /// <summary>Source mesh -> copy with smooth normals in UV3. Shared by every building using that mesh.</summary>
        static readonly Dictionary<Mesh, Mesh> SmoothedMeshes = new Dictionary<Mesh, Mesh>();

        readonly List<GameObject> hulls = new List<GameObject>();
        readonly HashSet<MeshRenderer> hullRenderers = new HashSet<MeshRenderer>();
        Material material;
        Material maskMaterial;

        public bool IsVisible { get; private set; }

        /// <summary>
        /// Shows the outline. <paramref name="outlineMaterial"/> draws the rim (colour and width live on it),
        /// <paramref name="mask"/> marks the building's own pixels so the rim never covers it.
        /// </summary>
        public void Show(Material outlineMaterial, Material mask)
        {
            if (outlineMaterial == null || mask == null) return;
            if (material != outlineMaterial || maskMaterial != mask || hulls.Count == 0) Rebuild(outlineMaterial, mask);

            IsVisible = true;
            foreach (GameObject hull in hulls)
                if (hull != null) hull.SetActive(true);
        }

        public void Hide()
        {
            IsVisible = false;
            foreach (GameObject hull in hulls)
                if (hull != null) hull.SetActive(false);
        }

        void OnDestroy()
        {
            foreach (GameObject hull in hulls)
                if (hull != null) Destroy(hull);
        }

        void Rebuild(Material outlineMaterial, Material mask)
        {
            foreach (GameObject hull in hulls)
                if (hull != null) Destroy(hull);
            hulls.Clear();
            material = outlineMaterial;
            maskMaterial = mask;

            foreach (MeshRenderer source in GetComponentsInChildren<MeshRenderer>(false))
            {
                if (hullRenderers.Contains(source)) continue; // our own hulls (old ones die at end of frame)
                if (!source.enabled) continue; // hidden helper meshes (footprints, collider proxies)
                MeshFilter filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Mesh mesh = GetSmoothedMesh(filter.sharedMesh);

                var hull = new GameObject("Outline Hull");
                hull.SetActive(false);
                hull.layer = source.gameObject.layer;
                hull.transform.SetParent(source.transform, false);

                hull.AddComponent<MeshFilter>().sharedMesh = mesh;
                var hullRenderer = hull.AddComponent<MeshRenderer>();
                hullRenderers.Add(hullRenderer);

                // Each submesh gets the mask and the outline. Unity draws extra materials on the last
                // submesh only, so with several submeshes we use one hull per role instead.
                hullRenderer.sharedMaterials = Fill(mesh.subMeshCount, outlineMaterial);
                GameObject maskHull = CreateMaskHull(hull.transform, mesh, mask);
                hullRenderers.Add(maskHull.GetComponent<MeshRenderer>());
                hullRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                hullRenderer.receiveShadows = false;
                hullRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                hullRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                hulls.Add(hull);
            }
        }

        static GameObject CreateMaskHull(Transform parent, Mesh mesh, Material mask)
        {
            var maskHull = new GameObject("Outline Mask");
            maskHull.layer = parent.gameObject.layer;
            maskHull.transform.SetParent(parent, false);
            maskHull.AddComponent<MeshFilter>().sharedMesh = mesh;
            var maskRenderer = maskHull.AddComponent<MeshRenderer>();
            maskRenderer.sharedMaterials = Fill(mesh.subMeshCount, mask);
            maskRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            maskRenderer.receiveShadows = false;
            maskRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            maskRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return maskHull;
        }

        static Material[] Fill(int subMeshCount, Material m)
        {
            var mats = new Material[Mathf.Max(1, subMeshCount)];
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            return mats;
        }

        /// <summary>
        /// Copy of the mesh with normals averaged across vertices that share a position, stored in UV3.
        /// Falls back to the original mesh (shader then uses regular normals) if the mesh isn't readable.
        /// </summary>
        static Mesh GetSmoothedMesh(Mesh source)
        {
            if (SmoothedMeshes.TryGetValue(source, out Mesh cached) && cached != null) return cached;

            if (!source.isReadable)
            {
                Debug.LogWarning($"BuildingOutline: mesh '{source.name}' isn't Read/Write enabled, outline may have gaps. " +
                                 "Tick Read/Write in the model's import settings.", source);
                SmoothedMeshes[source] = source;
                return source;
            }

            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            var smooth = new Vector3[vertices.Length];

            if (normals.Length == vertices.Length)
            {
                var sums = new Dictionary<Vector3, Vector3>(vertices.Length);
                for (int i = 0; i < vertices.Length; i++)
                {
                    sums.TryGetValue(vertices[i], out Vector3 sum);
                    sums[vertices[i]] = sum + normals[i];
                }
                for (int i = 0; i < vertices.Length; i++)
                    smooth[i] = sums[vertices[i]].normalized;
            }

            Mesh copy = Object.Instantiate(source);
            copy.name = source.name + " (outline)";
            copy.SetUVs(3, smooth);
            SmoothedMeshes[source] = copy;
            return copy;
        }
    }
}
