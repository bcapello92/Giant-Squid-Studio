using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public bool useOffset = true;   // can turn this off to hard-center
    public Vector3 manualOffset = new Vector3(0f, 0f, -10f); // fallback for 2D
    public float smooth = 0f;       // 0 = snap, >0 = smooth

    Vector3 _offset;
    bool _hasOffset = false;

    void Start()
    {
        if (target != null)
        {
            SetTarget(target);
        }
    }

    public void SetTarget(Transform t)
    {
        target = t;
        if (target == null) return;

        // keep camera's current Z
        float camZ = transform.position.z;

        if (useOffset)
        {
            _offset = transform.position - target.position;
            _offset.z = camZ - target.position.z;   // ensure z stays correct
            _hasOffset = true;
        }
        else
        {
            // hard center on target, with fixed z
            _offset = new Vector3(0f, 0f, camZ - target.position.z);
            _hasOffset = true;
            // snap immediately
            transform.position = target.position + _offset;
        }
    }

    void LateUpdate()
    {
        if (target == null || !_hasOffset) return;

        Vector3 wanted = target.position + _offset;

        if (smooth > 0f)
        {
            transform.position = Vector3.Lerp(transform.position, wanted, smooth * Time.deltaTime);
        }
        else
        {
            transform.position = wanted;
        }
    }
}
