using UnityEngine;
using ARFoundationReplay;

public class SerializedMeshTest : MonoBehaviour
{
    [SerializeField]
    MeshRenderer sourceRenderer;

    void Start()
    {
        var srcMesh = sourceRenderer.GetComponent<MeshFilter>().sharedMesh;
        var bytes = srcMesh.ToByteArray();

        Debug.Log($"bytes: {bytes.Length}");

        var dstMesh = new Mesh();
        dstMesh.UpdateFromBytes(bytes);

        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = sourceRenderer.sharedMaterial;
        var filter = gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = dstMesh;
    }

}
