using UnityEngine;

public class DoorPointManager : MonoBehaviour
{
    public string defaultDoorPointID;

    private void Start()
    {
        // Assuming you want to set a default door point at the start of the scene
        if (!string.IsNullOrEmpty(defaultDoorPointID))
        {
            GameManager.Instance.SetDoorPoint(defaultDoorPointID);
        }
    }
}
