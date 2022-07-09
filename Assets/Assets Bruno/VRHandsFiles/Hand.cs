using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class Hand : MonoBehaviour
{
    [SerializeField]
    private ActionBasedController m_Controller = null;
    [SerializeField]
    private Animator m_Animator = null;
    [SerializeField]
    private float m_Speed = 8.0f;

    private const string ANIMATOR_GRIP_PARAM = "Grip";

    private float m_GripTarget = 0.0f;
    private float m_CurGrip = 0.0f;


    // Update is called once per frame
    void Update()
    {
        m_GripTarget = m_Controller.selectAction.action.ReadValue<float>();

        if (m_CurGrip != m_GripTarget)
        {
            m_CurGrip = Mathf.MoveTowards(m_CurGrip, m_GripTarget, Time.deltaTime * m_Speed);
            m_Animator.SetFloat(ANIMATOR_GRIP_PARAM, m_CurGrip);
        }
    }
}
