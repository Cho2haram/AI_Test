using HapticDllCs;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class QuaternionMonitoring : MonoBehaviour
{
    private bool isMeasuring = false; // 데이터 측정 여부

    [Header ( "IMU" )]
    HapticDll hapticDll;
    public List<float> AccelList; // 가속도 데이터 (x, y, z)
    public List<float> GyroList; // 자이로스코프 데이터 (x, y, z 각속도)
    public List<float> QuatList; // 쿼터니언
    public List<float> EulerList; // 자이로스코프 데이터 (x, y, z 각속도)

    // Start is called before the first frame update
    void Start()
    {
        // 초기화
        hapticDll = new HapticDll ( );
        hapticDll. InitializeHapticDevice ( 0 , Debug. Log );



    }

    // Update is called once per frame
    void Update()
    {
        IMU_DataReceive ( ); // 데이터 받기

    }

    void IMU_DataReceive ( )
    {
        // IMU 데이터 가져오기
        AccelList = hapticDll. GetAccelerometer ( );
        GyroList = hapticDll. GetGyroscope ( );
        QuatList = hapticDll. GetQuaternionData ( );

        if ( AccelList. Count < 3 || GyroList. Count < 3 || QuatList. Count < 4 )
        {
            Debug. LogWarning ( "IMU data invalid, using defaults" );
            AccelList = new List<float> { 0 , 0 , 0 };
            GyroList = new List<float> { 0 , 0 , 0 };
            QuatList = new List<float> { 0 , 0 , 0 , 1 };
        }


        try
        {
            // QuatList에서 Quaternion 생성 (x, y, z, w 순서 가정)
            Quaternion quaternion = new Quaternion ( QuatList [ 1 ] , QuatList [ 2 ] , QuatList [ 3 ] , QuatList [ 0 ] );
            transform. rotation = quaternion;

        }
        catch ( System. Exception e )
        {
            Debug. LogError ( $"Failed to convert Quaternion to Euler: {e. Message}" );
            EulerList = new List<float> { 0 , 0 , 0 }; // 오류 시 기본값
        }
    }
}
