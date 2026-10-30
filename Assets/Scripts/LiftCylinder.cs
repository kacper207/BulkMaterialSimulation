using System.Collections;
using UnityEngine;

// Cylinder do testu podnoszonego cylindra (lifting cylinder test): napełniony cylinder stoi na podłodze,
// po ustabilizowaniu cząstek jest powoli podnoszony i materiał osiada w pryzmę bez uderzającego strumienia.
// Kinematyczny Rigidbody + MovePosition: PhysX zna prędkość ścian, więc tarcie o ściany działa poprawnie podczas ruchu.
[RequireComponent(typeof(Rigidbody))]
public class LiftCylinder : MonoBehaviour
{
    [SerializeField] float liftSpeed = 0.05f;
    [SerializeField] float liftHeight = 1.5f;

    Rigidbody body;
    Vector3 start;

    public float LiftSpeed => liftSpeed;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        start = body.position;
    }

    public void ResetDown() => body.position = start;

    public IEnumerator Lift()
    {
        var step = new WaitForFixedUpdate();
        float lifted = 0f;
        while (lifted < liftHeight)
        {
            lifted = Mathf.Min(lifted + liftSpeed * Time.fixedDeltaTime, liftHeight);
            body.MovePosition(start + Vector3.up * lifted);
            yield return step;
        }
    }
}
