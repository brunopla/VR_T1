using UnityEngine;

public class GrapplingGun : MonoBehaviour
{
    #region Variables

   // private LineRenderer lr;

    private float maxDistance = 100f;
    private SpringJoint joint;

    public Transform pivotStartPoint;
    public Transform gunTip,  player;
    private Vector3 grapplePoint;
    private Vector3 currentGrapplePosition;

    public LayerMask whatIsGrappleable;


    public float spring;
    public float damper;
    public float massScale;

    #endregion

    void Awake()
    {
    //    lr = GetComponent<LineRenderer>();
    }



    /// <summary>
    /// Call whenever we want to start a grapple
    /// </summary>
    public void StartGrapple()
    {
        RaycastHit hit;
        if (Physics.Raycast(pivotStartPoint.position, pivotStartPoint.forward, out hit, maxDistance, whatIsGrappleable))
        {
            grapplePoint = hit.point;
            joint = player.gameObject.AddComponent<SpringJoint>();
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = grapplePoint;

            float distanceFromPoint = Vector3.Distance(pivotStartPoint.position, grapplePoint);

            //The distance grapple will try to keep from grapple point. 
            joint.maxDistance = distanceFromPoint * 0.8f;
            joint.minDistance = distanceFromPoint * 0.25f;

            //Adjust these values to fit your game.
            joint.spring = spring;
            joint.damper = damper;
            joint.massScale = massScale;

           // lr.positionCount = 2;
            currentGrapplePosition = gunTip.position;

            DrawRope();
        }
    }


    /// <summary>
    /// Call whenever we want to stop a grapple
    /// </summary>
    public void StopGrapple()
    {
      //  lr.positionCount = 0;
        Destroy(joint);
    }



    void DrawRope()
    {
        //If not grappling, don't draw rope
        if (!joint) return;

        currentGrapplePosition = Vector3.Lerp(currentGrapplePosition, grapplePoint, Time.deltaTime * 8f);

     //   lr.SetPosition(0, gunTip.position);
     //   lr.SetPosition(1, currentGrapplePosition);
    }

    public bool IsGrappling()
    {
        return joint != null;
    }

    public Vector3 GetGrapplePoint()
    {
        return grapplePoint;
    }
}
