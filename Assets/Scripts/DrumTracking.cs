using System. Collections;
using System. Collections. Generic;
using UnityEngine;
using System. IO;
using static HapticDllCs. Common;
using HapticDllCs;


public class DrumTracking : MonoBehaviour
{
    HapticDll hapticDll;
    public int _hapticDirection; //1:left, 2:right
    public List<float> AccelList; // 가속도 데이터 (x, y, z)
    public List<float> GyroList; // 자이로스코프 데이터 (x, y, z)
    public List<float> QuatList; // 자이로스코프 데이터 (x, y, z)


    private List<string> csvData_imu = new List<string> ( );



  
    

    private bool isTracking = false;
    private bool hasTrackingData = false;

    private List<string> csvData = new List<string> ( );

    private Vector3 initialPosition;
    private float startTime;

    private Vector3 lastPosition;

    private float positionTolerance = 0.10f;
    private float rotationTolerance = 20f;
    private float closeToBodyThreshold = 0.50f;

    private string resultLabel = "UNKNOWN";



    void Start ( )
    {
        hapticDll = new HapticDll ( );
        hapticDll. InitializeHapticDevice ( _hapticDirection , Debug. Log );



        

      
    }

    void Update ( )
    {
        AccelList = hapticDll. GetAccelerometer ( );
        GyroList = hapticDll. GetGyroscope ( );
        QuatList = hapticDll. GetQuaternionData ( );


       

        if ( isTracking )
            Track ( );
            StartTracking ( );
    }

    void StartTracking ( )
    {
        

        isTracking = true;
        hasTrackingData = false;

        startTime = Time. time;

        csvData. Clear ( );
        csvData. Add ( "Time, PositionX, PositionY, PositionZ, Angle" );

        csvData_imu. Clear ( );
        csvData_imu. Add ( "Timestamp,Acc_X,Acc_Y,Acc_Z,Gyro_X,Gyro_Y,Gyro_Z,Quat_1,Quat_2,Quat_3,Quat_4" );


        Debug. Log ( "트래킹 시작" );
    }

   

    void Track ( )
    {
        float currentTime = Time. time - startTime;

       


        float a1 = 0.99996f;
        float a2 = -0.008973045f;
        float a3 = 0.0004364792f;
        float a4 = -0.0001839362f;

        // CSV에 시간, 위치, 회전 각도 기록
        csvData_imu. Add ( $"{currentTime}, {AccelList [ 0 ]},{AccelList [ 1 ]},{AccelList [ 2 ]},{GyroList [ 0 ]},{GyroList [ 1 ]},{GyroList [ 2 ]},{QuatList [ 0 ]},{QuatList [ 1 ]},{QuatList [ 2 ]},{QuatList [ 3 ]}" );

        hasTrackingData = true;
    }


    void SaveDataToCSV ( )
    {
        string timestamp = System. DateTime. Now. ToString ( "yyyyMMdd_HHmmss" );
        string directoryPath = @"C:\\Users\\admin\\Documents\\#DrumTrackingData";
        Directory. CreateDirectory ( directoryPath );

        string directoryPath2 = @"C:\Users\admin\Documents\#DrumTrackingData_IMU";
        Directory. CreateDirectory ( directoryPath2 );

        string fileName = $"tracking_{timestamp}_{resultLabel}.csv";
        string filePath = Path. Combine ( directoryPath , fileName );


        string fileName2 = $"trackingIMU_{timestamp}_{resultLabel}.csv";
        string filePath2 = Path. Combine ( directoryPath2 , fileName2 );

        File. WriteAllLines ( filePath , csvData );
        File. WriteAllLines ( filePath2 , csvData_imu );

        Debug. Log ( "CSV 저장 완료: " + filePath );
    }
}
