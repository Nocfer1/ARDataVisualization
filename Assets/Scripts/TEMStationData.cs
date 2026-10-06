using System;
using System.Collections.Generic;

[Serializable]
public class TEMStationData
{
    public int stationNumber;
    public List<float> resistivities = new List<float>();
    public List<float> thicknesses = new List<float>();
    public float doi;
}