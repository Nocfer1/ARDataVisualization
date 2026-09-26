using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    public Transform target;

    public float distance = 10f;
    public float zoomSpeed = 5f;
    public float rotationSpeed = 3f;

    public float minDistance = 2f;
    public float maxDistance = 50f;

    public float yaw = 30f;
    public float pitch = 25f;

    private Vector3 orbitCenter;

    private void Start()
    {
        RecalculateCenter();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Si quieres, recalcula cada frame mientras iteras.
        // Más adelante lo puedes quitar y recalcular solo al cargar.
        RecalculateCenter();

        if (Input.GetMouseButton(0))
        {
            yaw += Input.GetAxis("Mouse X") * rotationSpeed;
            pitch -= Input.GetAxis("Mouse Y") * rotationSpeed;
            pitch = Mathf.Clamp(pitch, -10f, 80f);
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            distance -= scroll * zoomSpeed * 10f;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -distance);

        transform.position = orbitCenter + offset;
        transform.LookAt(orbitCenter);
    }

    void RecalculateCenter()
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            orbitCenter = target.position;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            bounds.Encapsulate(r.bounds);
        }

        orbitCenter = bounds.center;

        // Ajuste automático de distancia inicial razonable
        float recommendedDistance = bounds.size.magnitude * 1.2f;
        if (distance < 0.1f || distance > recommendedDistance * 3f)
        {
            distance = Mathf.Clamp(recommendedDistance, minDistance, maxDistance);
        }
    }
}