using UnityEngine;

public class TriggerZone : MonoBehaviour
{
    public GameManager manager;

    private void OnTriggerEnter(Collider other)
    {
        CubeClickHandler cubeHandler = other.GetComponent<CubeClickHandler>();
        if (cubeHandler != null)
        {
            manager.OnCubeMissed(cubeHandler.cubeIndex);
            Destroy(other.gameObject);
        }
    }
}