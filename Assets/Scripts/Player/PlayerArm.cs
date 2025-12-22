using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Blocky first-person arms similar to Minecraft.
/// - Creates left/right arms as simple cube parts
/// - Handles idle bobbing, mouse swing, and click swing animation
/// - Attempt to render visibly in front of the camera
/// </summary>
public class PlayerArm : MonoBehaviour
{
    [Header("Arm Settings")]
    public Transform armPivot;
    public bool showBothArms = false; // Set true to show both arms
    public Color skinColor = new Color(1f, 0.8f, 0.6f);

    [Header("Animation")]
    public float bobSpeed = 6f;
    public float bobAmount = 0.02f;
    public float swaySpeed = 3f;
    public float swayAmount = 3f;

    [Header("Swing")]
    public float swingSpeed = 18f;
    public float swingAngle = 70f;

    private Transform leftArmRoot;
    private Transform rightArmRoot;

    private Quaternion leftInitialRot;
    private Quaternion rightInitialRot;

    private bool isSwinging = false;
    private float swingProgress = 0f;

    private Vector3 initialLocalPos;

    public Transform GetRightArm()
    {
        return rightArmRoot;
    }

    void Awake()
    {
        if (armPivot == null)
        {
            CreateArmPivot();
        }

        if (leftArmRoot == null || rightArmRoot == null)
        {
            BuildArms();
        }

        leftInitialRot = leftArmRoot.localRotation;
        rightInitialRot = rightArmRoot.localRotation;
        initialLocalPos = armPivot.localPosition;

        // Default: hide left arm for single-handed first person like Minecraft
        if (!showBothArms)
            leftArmRoot.gameObject.SetActive(false);
    }

    // Removed Start() as it is replaced by Awake()


    void Update()
    {
        // Bob based on player movement input
        float move = Mathf.Abs(Input.GetAxis("Horizontal")) + Mathf.Abs(Input.GetAxis("Vertical"));
        float bobFactor = Mathf.Clamp01(move);

        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount * (0.5f + bobFactor);
        armPivot.localPosition = initialLocalPos + new Vector3(0, bob, 0);

        // Sway with mouse X
        float mouseX = Input.GetAxis("Mouse X");
        float sway = -mouseX * swayAmount;
        armPivot.localRotation = Quaternion.Euler(sway, 0, 0);

        // Swing while holding the left mouse button (continuous)
        if (Input.GetMouseButton(0))
        {
            isSwinging = true;
        }
        else
        {
            // Not holding: stop the continuous swing so we can return to rest
            isSwinging = false;
        }

        if (isSwinging)
        {
            // Advance the swing continuously while held
            swingProgress += Time.deltaTime * swingSpeed;
            // Keep the value bounded to avoid float drift
            if (swingProgress > Mathf.PI * 2f) swingProgress -= Mathf.PI * 2f;

            float t = Mathf.Sin(swingProgress);
            float a = t * swingAngle;

            // Apply to visible arm(s)
            if (rightArmRoot != null)
                rightArmRoot.localRotation = rightInitialRot * Quaternion.Euler(-a * 0.6f, -a * 0.2f, a * 0.2f);
            if (leftArmRoot != null && showBothArms)
                leftArmRoot.localRotation = leftInitialRot * Quaternion.Euler(-a * 0.6f, a * 0.2f, -a * 0.2f);
        }
        else
        {
            // Smoothly return to the rest pose when released
            if (rightArmRoot != null)
                rightArmRoot.localRotation = Quaternion.Slerp(rightArmRoot.localRotation, rightInitialRot, Time.deltaTime * swingSpeed * 0.5f);
            if (leftArmRoot != null && showBothArms)
                leftArmRoot.localRotation = Quaternion.Slerp(leftArmRoot.localRotation, leftInitialRot, Time.deltaTime * swingSpeed * 0.5f);

            // Damp the swing progress back toward zero
            if (swingProgress > 0f)
            {
                swingProgress = Mathf.Max(0f, swingProgress - Time.deltaTime * swingSpeed);
            }
        }
    }

    void CreateArmPivot()
    {
        GameObject pivotObj = new GameObject("ArmPivot");
        pivotObj.transform.SetParent(transform, false);
        // Position arms slightly to the right and down like typical first-person
        pivotObj.transform.localPosition = new Vector3(0.4f, -0.3f, 0.5f);
        pivotObj.transform.localRotation = Quaternion.identity;
        armPivot = pivotObj.transform;
    }

    void BuildArms()
    {
        // Create materials
        // Use Custom/OverlayLit to draw on top of everything (ZTest Always)
        Shader overlayShader = Shader.Find("Custom/OverlayLit");
        if (overlayShader == null) overlayShader = Shader.Find("Standard"); // Fallback

        Material skinMat = new Material(overlayShader);
        skinMat.color = skinColor;
        // skinMat.renderQueue = 4000; // Shader already sets Queue=Overlay

        // Right arm
        GameObject rightRoot = new GameObject("RightArmRoot");
        rightRoot.transform.SetParent(armPivot, false);
        rightRoot.transform.localPosition = new Vector3(0.18f, -0.05f, 0.35f);
        rightRoot.transform.localRotation = Quaternion.Euler(10, -20, 0);

        // Upper arm
        GameObject rUpper = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rUpper.name = "RightUpper";
        rUpper.transform.SetParent(rightRoot.transform, false);
        rUpper.transform.localPosition = new Vector3(0, -0.06f, 0);
        rUpper.transform.localScale = new Vector3(0.16f, 0.24f, 0.18f);
        var rUpR = rUpper.GetComponent<Renderer>();
        rUpR.sharedMaterial = skinMat;
        rUpR.shadowCastingMode = ShadowCastingMode.Off;
        rUpR.receiveShadows = false;
        Destroy(rUpper.GetComponent<Collider>());

        // Lower arm / hand
        GameObject rLower = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rLower.name = "RightLower";
        rLower.transform.SetParent(rightRoot.transform, false);
        rLower.transform.localPosition = new Vector3(0, -0.06f, 0.18f);
        rLower.transform.localScale = new Vector3(0.14f, 0.18f, 0.36f);
        var rLowR = rLower.GetComponent<Renderer>();
        rLowR.sharedMaterial = skinMat;
        rLowR.shadowCastingMode = ShadowCastingMode.Off;
        rLowR.receiveShadows = false;
        Destroy(rLower.GetComponent<Collider>());

        // Left arm (mirrored)
        GameObject leftRoot = new GameObject("LeftArmRoot");
        leftRoot.transform.SetParent(armPivot, false);
        leftRoot.transform.localPosition = new Vector3(-0.06f, -0.05f, 0.25f);
        leftRoot.transform.localRotation = Quaternion.Euler(10, 10, 0);

        GameObject lUpper = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lUpper.name = "LeftUpper";
        lUpper.transform.SetParent(leftRoot.transform, false);
        lUpper.transform.localPosition = new Vector3(0, -0.06f, 0);
        lUpper.transform.localScale = new Vector3(0.16f, 0.24f, 0.18f);
        var lUpR = lUpper.GetComponent<Renderer>();
        lUpR.sharedMaterial = skinMat;
        lUpR.shadowCastingMode = ShadowCastingMode.Off;
        lUpR.receiveShadows = false;
        Destroy(lUpper.GetComponent<Collider>());

        GameObject lLower = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lLower.name = "LeftLower";
        lLower.transform.SetParent(leftRoot.transform, false);
        lLower.transform.localPosition = new Vector3(0, -0.06f, 0.18f);
        lLower.transform.localScale = new Vector3(0.14f, 0.18f, 0.36f);
        var lLowR = lLower.GetComponent<Renderer>();
        lLowR.sharedMaterial = skinMat;
        lLowR.shadowCastingMode = ShadowCastingMode.Off;
        lLowR.receiveShadows = false;
        Destroy(lLower.GetComponent<Collider>());

        rightArmRoot = rightRoot.transform;
        leftArmRoot = leftRoot.transform;
    }

    void StartSwing()
    {
        if (!isSwinging)
        {
            isSwinging = true;
            swingProgress = 0f;
        }
    }

    public void Swing()
    {
        StartSwing();
    }
}

