using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;

[RequireComponent(typeof(ParentConstraint))]
public class MainCameraAutoParent : MonoBehaviour
{
    public Vector3 offset = new Vector3(0,0,1);
    private Vector3 originalPosition;
    private ParentConstraint constraint;
    void Start()
    {
        originalPosition = transform.position;
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        constraint = GetComponent<ParentConstraint>();
        
        constraint.SetSources(new System.Collections.Generic.List<ConstraintSource>());
        
        if (Camera.main != null)
        {
            ConstraintSource source = new ConstraintSource();
            source.sourceTransform = Camera.main.transform;
            source.weight = 1f;
            
            constraint.AddSource(source);
            
            constraint.constraintActive = true;
            constraint.SetTranslationOffset(0, offset);
            constraint.locked = true;
        }
        else
        {
            Debug.LogError("No MainCamera found in the scene.");
        }
    }

    void Update()
    {
        constraint.SetTranslationOffset(0, offset);
    }
}
