using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TEMStationData
{
    public int stationNumber;
    public List<float> resistivities = new List<float>();
    public List<float> thicknesses = new List<float>();
    public float doi;
    
    public Vector3 worldPosition; // (X: east west, Y: north south, Z: up down)
    public Vector3 localPosition; // Pre-calculated Unity local position centered around (0,0,0)
}