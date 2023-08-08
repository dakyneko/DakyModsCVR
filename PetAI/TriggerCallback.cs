using System;
using UnityEngine;

public class TriggerCallback : MonoBehaviour
{
    public event Listener? EnterListener, ExitListener;
    public delegate void Listener(Collider other);

    private void OnTriggerEnter(Collider other) => EnterListener?.Invoke(other);
    private void OnTriggerExit(Collider collider) => ExitListener?.Invoke(collider);
}
