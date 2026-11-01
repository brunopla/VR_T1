using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class Hand : MonoBehaviour
{
    // El rig del proyecto usa los controladores de XRI 2.x (obsoletos en XRI 3)
#pragma warning disable 618
    [SerializeField]
    private ActionBasedController m_Controller = null;
#pragma warning restore 618
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
