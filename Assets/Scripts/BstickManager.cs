using HapticDllCs;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BstickManager : MonoBehaviour
{
    public static BstickManager Instance { get; private set; }
    private HapticDll hapticDll;
    public List<float> AccelList { get; private set; } = new List<float> ( ); // 초기화
    public List<float> GyroList { get; private set; } = new List<float> ( ); // 초기화
    public List<float> QuatList { get; private set; } = new List<float> ( ); // 초기화
    public List<float> EulerList { get; private set; } = new List<float> ( ); // Euler 각도 저장용 리스트 추가

    private bool isInitialized = false;

    public bool isTracking = false; //스페이스바 컨트롤용


    void Awake ( )
    {
        if ( Instance == null )
        {
            Instance = this;

            InitializeHaptic ( );
        }

    }

    private void InitializeHaptic ( )
    {
        if ( !isInitialized )
        {
            hapticDll = new HapticDll ( );
            try
            {
                hapticDll. InitializeHapticDevice ( 0 , Debug. Log );
                isInitialized = true;
                Debug. Log ( "HapticDll initialized successfully" );
            }
            catch ( System. Exception e )
            {
                Debug. LogError ( $"Failed to initialize HapticDll: {e. Message}" );
            }
        }
    }

    public bool TouchButtonRelease ( )
    {
        if ( hapticDll == null || !isInitialized )
        {
            Debug. LogWarning ( "HapticDll is null or not initialized, reinitializing..." );
            InitializeHaptic ( );
        }
        try
        {
            return hapticDll. TouchButtonRelease ( );
        }
        catch ( System. Exception e )
        {
            Debug. LogError ( $"TouchButtonRelease failed: {e. Message}" );
            return false;
        }

    }

    //스페이스바 컨트롤용
    public void Update ( )
    {
        // 스페이스바 눌렀을 때 측정 시작/종료 토글
        if ( Input. GetKeyDown ( KeyCode. Space ) )
        {
            isTracking = !isTracking;
            Debug. Log ( isTracking ? "측정 시작" : "측정 종료" );
        }

        // 측정 중일 때만 IMU 데이터 수신
        if ( isTracking )
        {
            if ( hapticDll != null && isInitialized )
            {
                IMU_DataReceive ( );
            }
            else
            {
                Debug. LogWarning ( "HapticDll이 준비되지 않았습니다. 초기화를 시도합니다..." );
                InitializeHaptic ( );  // 초기화만 시도, 데이터 측정은 다음 프레임부터
            }
        }
    }


    public void IMU_DataReceive ( )
    {
        if ( hapticDll == null || !isInitialized )
        {
            Debug. LogWarning ( "HapticDll not ready, reinitializing..." );
            InitializeHaptic ( );
        } ;


        AccelList = hapticDll. GetAccelerometer ( ) ?? new List<float> { 0 , 0 , 0 };
        GyroList = hapticDll. GetGyroscope ( ) ?? new List<float> { 0 , 0 , 0 };
        QuatList = hapticDll. GetQuaternionData ( ) ?? new List<float> { 0 , 0 , 0 , 1 };

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
            Quaternion quaternion = new Quaternion ( QuatList [ 0 ] , QuatList [ 1 ] , QuatList [ 2 ] , QuatList [ 3 ] );
            transform. rotation = quaternion;

            // Euler 각도로 변환
            Vector3 eulerAngles = quaternion. eulerAngles;

            // EulerList에 저장 (x, y, z)
            EulerList = new List<float> { eulerAngles. x , eulerAngles. y , eulerAngles. z };

            // 디버그 출력 (선택 사항)
            //Debug. Log ( $"Euler Angles: X={eulerAngles. x}, Y={eulerAngles. y}, Z={eulerAngles. z}" );
        }
        catch ( System. Exception e )
        {
            Debug. LogError ( $"Failed to convert Quaternion to Euler: {e. Message}" );
            EulerList = new List<float> { 0 , 0 , 0 }; // 오류 시 기본값
        }

    }
    //private void OnDisable ( )
    //{
    //    hapticDll. CloseClient ( );
    //}

    //private void OnEnable ( )
    //{
    //    hapticDll. InitializeHapticDevice ( 0 , Debug. Log );
    //}

    void OnDestroy ( )
    {
        if ( Instance == this )
        {
            hapticDll. CloseClient ( );
            isInitialized = false;
            Instance = null;
        }
    }
}
