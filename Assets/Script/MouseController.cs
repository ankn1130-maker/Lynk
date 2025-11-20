//using System;
//using System.Collections.Generic;
//using UnityEditor.PackageManager.UI;
using UnityEngine;
//using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif
public class MouseController : MonoBehaviour
{
    BoardGrid3D board;
    Camera cam;

    void Start()
    {
        board = FindObjectOfType<BoardGrid3D>();
        cam = Camera.main;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (board.PlacePiece(hit.point)) Debug.Log("Placed!");
            }
        }

        // Hover highlight
        Ray hoverRay = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(hoverRay, out RaycastHit hoverHit))
        {
            Vector3Int gp = board.WorldToGridPos(hoverHit.point);
            board.ShowHighlight(gp);
        }
        else board.HideHighlight();
    }
}
