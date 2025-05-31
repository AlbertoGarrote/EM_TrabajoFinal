
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

//MANTIENE UN CANVAS MIRANDO A LA CÁMARA
public class FloatingText : MonoBehaviour
{
    Transform cam;
    public TMP_Text text;
    // Start is called before the first frame update
    private void Start()
    {

        cam = GameObject.FindObjectOfType<Camera>().transform;
        
    }

    private void Awake()
    {
        text = GetComponentInChildren<TMP_Text>();
    }

    void FixedUpdate()
    {
        transform.forward = cam.forward;
    }
}
