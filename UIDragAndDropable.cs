// Drag and Drop support for UI elements. Drag and Drop actions will be sent to
// the local player GameObject.
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIDragAndDropable : MonoBehaviour , IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    // drag options
    public PointerEventData.InputButton button = PointerEventData.InputButton.Left;
    public GameObject drageePrefab;
    public static GameObject currentlyDragged;

    // status
    public bool dragable = true;
    public bool dropable = true;

    [HideInInspector] public bool draggedToSlot = false;

    public void OnBeginDrag(PointerEventData d)
    {
        // one mouse button is enough for dnd
        if (dragable && d.button == button)
        {
            // load current
            currentlyDragged = Instantiate(drageePrefab, transform.position, Quaternion.identity);
            currentlyDragged.GetComponent<Image>().sprite = GetComponent<Image>().sprite;
            currentlyDragged.transform.SetParent(transform.root, true); // canvas
            currentlyDragged.transform.SetAsLastSibling(); // move to foreground

            // disable button while dragging so onClick isn't fired if we drop a
            // slot on itself
            GetComponent<Button>().interactable = false;
        }
    }

    public void OnDrag(PointerEventData d)
    {
        // one mouse button is enough for drag and drop
        if (dragable && d.button == button)
            // move current
            currentlyDragged.transform.position = d.position;
    }

    // called after the slot's OnDrop
    public void OnEndDrag(PointerEventData d)
    {
        // delete current in any case
        Destroy(currentlyDragged);

        // one mouse button is enough for drag and drop
        if (dragable && d.button == button)
        {
            // try destroy if not dragged to a slot (flag will be set by slot)
            // message is sent to drag and drop handler for game specifics
            // -> only if dropping it into nirvana. do nothing if we just drop
            //    it on a panel. otherwise item slots are cleared if we
            //    accidentally drop it on the panel between two slots
            if (!draggedToSlot && d.pointerEnter == null)
            {
                // send a drag and clear message like
                // OnDragAndClear_Skillbar({index})
                Player.localPlayer.SendMessage("OnDragAndClear_" + tag,
                                               name.ToInt(),
                                               SendMessageOptions.DontRequireReceiver);
            }

            // reset flag
            draggedToSlot = false;

            // enable button again
            GetComponent<Button>().interactable = true;
        }
    }

    // d.pointerDrag is the object that was dragged
    public void OnDrop(PointerEventData d)
    {
        if (dropable && d.button == button)
        {
            UIDragAndDropable dropDragable = d.pointerDrag.GetComponent<UIDragAndDropable>();
            if (dropDragable != null && dropDragable.dragable)
            {
                dropDragable.draggedToSlot = true;

                int from = dropDragable.name.ToInt();
                int to = name.ToInt();

                Debug.Log($"🟢 Dragging from {dropDragable.tag} slot {from} to {tag} slot {to}");

                // Determine the correct function to call based on the tag
                string dragTag = dropDragable.tag;
                string dropTag = tag;
                string functionName = $"OnDragAndDrop_{dragTag}_{dropTag}";

                Debug.Log($"🔄 Sending message: {functionName}");

                // Send a message that matches the dragging source and target
                Player.localPlayer.SendMessage(functionName,
                                               new int[] { from, to },
                                               SendMessageOptions.DontRequireReceiver);
            }
        }
    }


    void OnDisable()
    {
        Destroy(currentlyDragged);
    }

    void OnDestroy()
    {
        Destroy(currentlyDragged);
    }
}
