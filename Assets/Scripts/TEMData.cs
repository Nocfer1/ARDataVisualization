[System.Serializable]
public struct TEMData
{
    public int stationCount;
    public float stationSpacing;
    public float sectionWidth;
    public float[] layerThicknesses;
    
    // 2D Array: resistivity[stationIndex, layerIndex]
    // CSV files will populate explicit values per cell directly here
    public float[,] resistivities; 
}