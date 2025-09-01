using UnityEngine;
using Unity. Sentis;
using System. Collections. Generic;
using HapticDllCs;
using System. IO;
using System. Linq;
using TMPro;

public class Raninbow_space : MonoBehaviour
{
    [Header ( "Prediction Result UI" )]
    public TextMeshProUGUI resultText; //예측 결과 텍스트
    public TextMeshProUGUI averagePredictionText; //예측 결과 평균 확률
    public TextMeshProUGUI measuringText; //측정 중 텍스트
    public TextMeshProUGUI timerText; // UI에 시간 표시


    [Header ( "IMU Data" )]
    HapticDll hapticDll;
    public List<float> AccelList; // 3축 가속도 데이터 (x, y, z)
    public List<float> GyroList; // 3축 각속도 데이터 (x, y, z)
    public List<float> QuatList; // 쿼터니언
    private List<float [ ]> imuDataList = new List<float [ ]> ( );
    private List<float [ ]> collectedIMUData = new List<float [ ]> ( ); // 측정된 데이터를 저장할 리스트
    public Vector3 velocity = Vector3. zero;
    public Vector3 position = Vector3. zero;
    private float startTime;
    private float lastTime;
    private bool isFirstFrame = true;
    private Vector3 lastPosition = Vector3. zero;
    private List<float> deltaTimes = new List<float> ( ); // 디버깅용 deltaTime 저장
    private Quaternion startQuat = Quaternion. identity;
    private Quaternion quatBase = Quaternion. identity;
    private bool isFirstQuatFrame = true;
    private Quaternion quatPrevious = Quaternion. identity;
    private Quaternion quatCurrent = Quaternion. identity;


    [Header ( "CSV" )]
    public string saveFilePath = "C:\\Users\\HARAM\\Desktop\\BsitckData";
    private bool isMeasuring = false; // 데이터 측정 여부
    private int fileCount = 1; // 파일 이름에 사용할 카운터

    [Header ( "AI" )]
    public ModelAsset rainbowPredictionModel;  // ONNX 모델
    Worker worker;
    Tensor<float> tensor;
    private const int windowSize = 20;  // LSTM 입력 시퀀스 길이
    private const int targetLength = 953; // 학습에 사용한 보간 데이터 길이
    private const int numFeatures = 10; //학습에 사용된 피처의 개수 = Timestamp, Acc x, y, z, Gyro x, y, z
    private readonly float [ ] means = new float [ numFeatures ] { 2.27412869f,   0.07317171f,  -0.22420879f,  -0.29424542f,  -8.23459375f,
  14.38256792f, -26.35567906f,  31.47853422f, -26.96594501f,  26.42408198f };
    private readonly float [ ] stdDevs = new float [ numFeatures ] { 1.34602494f,   0.79940245f,   0.39876798f,   0.33870973f,  76.42621665f,
 124.92598076f,  59.68352286f,  23.854101f,    66.58859878f, 100.79731164f };

    void Start ( )
    {
        // Sentis ONNX 모델 로드
        var runtimeModel = ModelLoader. Load ( rainbowPredictionModel );
        worker = new Worker ( runtimeModel , BackendType. CPU );

        // 초기 UI 설정
        if ( measuringText != null )
        {
            measuringText. text = "데이터를 측정 중입니다...";
            measuringText. gameObject. SetActive ( false ); // 시작 시 비활성화
        }
        if ( resultText != null )
        {
            resultText. gameObject. SetActive ( false ); // 시작 시 비활성화
        }
        if ( averagePredictionText != null )
        {
            averagePredictionText. gameObject. SetActive ( false ); // 시작 시 비활성화
        }

    }

    void Update ( )
    {
        if ( BstickManager. Instance == null )
        {
            Debug. LogError ( "BstickManager.Instance is null!" );
            return;
        }

        //스페이스바 컨트롤용
        if ( BstickManager. Instance. isTracking )
        {
            if ( !isMeasuring )
            {
                ToggleMeasurement ( );  // 측정 시작
            }

            CollectIMUData ( );


        }
        else
        {
            if ( isMeasuring )
            {
                ToggleMeasurement ( );  // 측정 종료
            }
        }
    }

    void ResetData ( )
    {
        quatBase = Quaternion. identity;
        quatCurrent = Quaternion. identity;
        quatPrevious = Quaternion. identity;

        isFirstFrame = true;
        isFirstQuatFrame = true;
    }

    void ToggleMeasurement ( )
    {
        if ( !isMeasuring )
        {
            startTime = Time. time;
            lastTime = 0f;

            isMeasuring = true;
            collectedIMUData. Clear ( );
            imuDataList. Clear ( );
            ResetData ( );

            // UI 업데이트: measuringText 활성화, resultText 비활성화
            if ( measuringText != null )
            {
                measuringText. gameObject. SetActive ( true );
                timerText. text = "0초"; // 초기 타이머 텍스트
            }
            if ( resultText != null )
            {
                resultText. gameObject. SetActive ( false );
            }
            if ( averagePredictionText != null )
            {
                averagePredictionText. gameObject. SetActive ( false );
            }


            BstickManager. Instance. IMU_DataReceive ( );
            Debug. Log ( "측정 시작" );
        }
        else
        {
            isMeasuring = false;
            SaveRawDataToCSV ( );
            Debug. Log ( $"측정 종료, 예측 시작. 수집된 데이터 포인트: {collectedIMUData. Count}" );

            // UI 업데이트: measuringText 비활성화
            if ( measuringText != null )
            {
                measuringText. gameObject. SetActive ( false );
            }

            if ( deltaTimes. Count > 0 )
            {
                float avgDeltaTime = deltaTimes. Average ( );
                Debug. Log ( $"평균 DeltaTime: {avgDeltaTime:F4}초 (샘플링 주파수: {1f / avgDeltaTime:F1}Hz)" );
            }
            if ( collectedIMUData. Count >= windowSize )
            {
                PredictSuccess ( );
            }
            else
            {
                Debug. LogWarning ( "데이터가 충분하지 않습니다." );
            }
        }
    }

    void CollectIMUData ( )
    {
        BstickManager. Instance. IMU_DataReceive ( );
        var accelList = BstickManager. Instance. AccelList;
        var gyroList = BstickManager. Instance. GyroList;
        var quatList = BstickManager. Instance. QuatList;

        if ( accelList == null || gyroList == null || accelList. Count < 3 || gyroList. Count < 3 || quatList == null || quatList. Count < 3 )
        {
            Debug. LogError ( "IMU data is invalid!" );
            return;
        }

        float currentTime = Time. time - startTime;
        float deltaTime = currentTime - lastTime;

        if ( timerText != null )
        {
            timerText. text = $"{Mathf. FloorToInt ( currentTime )}초";
        }

        Vector3 acceleration = new Vector3 ( accelList [ 0 ] - 1.0f , accelList [ 1 ] , accelList [ 2 ] );
        // 쿼터니언 받아오기 (Bstick 순서 → Unity)
        Quaternion quatRaw = new Quaternion ( quatList [ 1 ] , quatList [ 2 ] , quatList [ 3 ] , quatList [ 0 ] );
        Quaternion quatUnity = ConvertIMUToUnity ( Canonicalize ( quatRaw ) );

        if ( isFirstQuatFrame )
        {
            startQuat = quatUnity;
            quatBase = Quaternion. Inverse ( startQuat );
            quatPrevious = quatBase * quatUnity;
            isFirstQuatFrame = false;
        }

        Quaternion quat = quatBase * quatUnity;


        // Euler 변환
        Vector3 eulerAngles = quat. eulerAngles;
        eulerAngles. x = Mathf. DeltaAngle ( 0f , eulerAngles. x );
        eulerAngles. y = Mathf. DeltaAngle ( 0f , eulerAngles. y );
        eulerAngles. z = Mathf. DeltaAngle ( 0f , eulerAngles. z );

        // 결과값 저장
        float [ ] imuFrame = {
            currentTime,
        accelList[0], accelList[1], accelList[2],
        gyroList[0], gyroList[1], gyroList[2],
        eulerAngles.x, eulerAngles.y, eulerAngles.z
        };


        imuDataList. Add ( imuFrame );
        collectedIMUData. Add ( imuFrame );

        ChangeDataAfterCalculate ( );


        void ChangeDataAfterCalculate ( )
        {
            lastTime = currentTime;
            lastPosition = position;
            quatPrevious = quat;
        }
    }

    // 쿼터니언 정규화
    public Quaternion Canonicalize ( float x , float y , float z , float w )
    {
        Quaternion quat = new Quaternion ( x , y , z , w ); // x, y, z, w 변환
        quat. Normalize ( );

        return quat;
    }

    public Quaternion Canonicalize ( Quaternion quat )
    {
        quat. Normalize ( );

        if ( Quaternion. Dot ( quatPrevious , quat ) < 0f )
        {
            quat = new Quaternion ( -quat. x , -quat. y , -quat. z , -quat. w );
        }

        return quat;
    }

    // IMU → Unity 좌표계 변환
    Quaternion ConvertIMUToUnity ( Quaternion imuQ )
    {
        Quaternion qLeftHand = new Quaternion ( imuQ. x , imuQ. y , -imuQ. z , -imuQ. w );
        //return new Quaternion ( -qLeftHand. z , qLeftHand. x , qLeftHand. y , qLeftHand. w );
        return new Quaternion ( -qLeftHand. y , qLeftHand. x , qLeftHand. z , qLeftHand. w );



    }


    void SaveRawDataToCSV ( )
    {
        try
        {
            if ( imuDataList. Count == 0 )
            {
                Debug. LogError ( "imuDataList가 비어 있습니다." );
                return;
            }

            int index = 0;
            string baseFileName = "Drum_prediction_data";
            string fileName = Path. Combine ( saveFilePath , baseFileName + ".csv" );

            // 파일이 존재하면 _1, _2, _3... 붙이기
            while ( File. Exists ( fileName ) )
            {
                index++;
                fileName = Path. Combine ( saveFilePath , $"{baseFileName}_{index}.csv" );
            }

            using ( StreamWriter writer = new StreamWriter ( fileName , false ) )
            {
                //writer. WriteLine ( "Timestamp,Acc_X,Acc_Y,Gyro_X,Gyro_Y,Gyro_Z, Pos_X, Pos_Y, Pos_Z, Dist_X, Dist_Y, Dist_Z" );
                writer. WriteLine ( "Timestamp,Acc_X,Acc_Y,Acc_Z,Gyro_X,Gyro_Y,Gyro_Z,Euler_x,Euler_y,Euler_z" );


                foreach ( float [ ] data in imuDataList )
                {
                    writer. WriteLine ( $"{data [ 0 ]},{data [ 1 ]},{data [ 2 ]},{data [ 3 ]},{data [ 4 ]},{data [ 5 ]},{data [ 6 ]},{data [ 7 ]},{data [ 8 ]}, {data [ 9 ]}" );
                }
            }
            Debug. Log ( $"원시 데이터 저장 완료: {fileName}" );

            // 위치 데이터 범위 디버깅
            float minPosX = float. MaxValue, maxPosX = float. MinValue;
            float minPosY = float. MaxValue, maxPosY = float. MinValue;
            float minPosZ = float. MaxValue, maxPosZ = float. MinValue;
            foreach ( var data in imuDataList )
            {
                minPosX = Mathf. Min ( minPosX , data [ 7 ] );
                maxPosX = Mathf. Max ( maxPosX , data [ 7 ] );
                minPosY = Mathf. Min ( minPosY , data [ 8 ] );
                maxPosY = Mathf. Max ( maxPosY , data [ 8 ] );
                minPosZ = Mathf. Min ( minPosZ , data [ 9 ] );
                maxPosZ = Mathf. Max ( maxPosZ , data [ 9 ] );
            }
            Debug. Log ( $"원시 위치 데이터 범위: Pos_X=({minPosX:F2},{maxPosX:F2}), Pos_Y=({minPosY:F2},{maxPosY:F2}), Pos_Z=({minPosZ:F2},{maxPosZ:F2})" );
        }
        catch ( System. Exception e )
        {
            Debug. LogError ( $"원시 데이터 저장 실패: {e. Message}" );
        }
    }


    List<float [ ]> InterpolateToTargetLength ( List<float [ ]> dataList , int targetLength )
    {
        int currentLength = dataList. Count;
        List<float [ ]> interpolatedData = new List<float [ ]> ( targetLength );

        // 보간하려는 비율 계산
        float stepSize = ( float ) ( currentLength - 1 ) / ( targetLength - 1 );

        for ( int i = 0 ; i < targetLength ; i++ )
        {
            int index1 = Mathf. FloorToInt ( i * stepSize );
            int index2 = Mathf. Min ( index1 + 1 , currentLength - 1 );
            float t = ( i * stepSize ) - index1;

            //Acc, Gyro, Euler 데이터 보간
            float interpolatedTime = Mathf. Lerp ( dataList [ index1 ] [ 0 ] , dataList [ index2 ] [ 0 ] , t );
            float interpolatedAccelX = Mathf. Lerp ( dataList [ index1 ] [ 1 ] , dataList [ index2 ] [ 1 ] , t );
            float interpolatedAccelY = Mathf. Lerp ( dataList [ index1 ] [ 2 ] , dataList [ index2 ] [ 2 ] , t );
            float interpolatedAccelZ = Mathf. Lerp ( dataList [ index1 ] [ 3 ] , dataList [ index2 ] [ 3 ] , t );
            float interpolatedGyroX = Mathf. Lerp ( dataList [ index1 ] [ 4 ] , dataList [ index2 ] [ 4 ] , t );
            float interpolatedGyroY = Mathf. Lerp ( dataList [ index1 ] [ 5 ] , dataList [ index2 ] [ 5 ] , t );
            float interpolatedGyroZ = Mathf. Lerp ( dataList [ index1 ] [ 6 ] , dataList [ index2 ] [ 6 ] , t );
            float interpolatedeulerX = Mathf. Lerp ( dataList [ index1 ] [ 7 ] , dataList [ index2 ] [ 7 ] , t );
            float interpolatedeulerY = Mathf. Lerp ( dataList [ index1 ] [ 8 ] , dataList [ index2 ] [ 8 ] , t );
            float interpolatedeulerZ = Mathf. Lerp ( dataList [ index1 ] [ 9 ] , dataList [ index2 ] [ 9 ] , t );

            // 보간된 값을 새로운 프레임에 추가
            float [ ] interpolatedFrame = new float [ 10 ];
            interpolatedFrame [ 0 ] = interpolatedTime;
            interpolatedFrame [ 1 ] = interpolatedAccelX;
            interpolatedFrame [ 2 ] = interpolatedAccelY;
            interpolatedFrame [ 3 ] = interpolatedAccelZ;
            interpolatedFrame [ 4 ] = interpolatedGyroX;
            interpolatedFrame [ 5 ] = interpolatedGyroY;
            interpolatedFrame [ 6 ] = interpolatedGyroZ;
            interpolatedFrame [ 7 ] = interpolatedeulerX;
            interpolatedFrame [ 8 ] = interpolatedeulerY;
            interpolatedFrame [ 9 ] = interpolatedeulerZ;


            interpolatedData. Add ( interpolatedFrame );
        }

        Debug. Log ( $"보간된 데이터: 첫 프레임={string. Join ( "," , interpolatedData [ 0 ]. Select ( x => x. ToString ( "F2" ) ) )}, " +
                  $"마지막 프레임={string. Join ( "," , interpolatedData [ targetLength - 1 ]. Select ( x => x. ToString ( "F2" ) ) )}" );


        return interpolatedData;
    }

    // ✅ 표준화 함수 추가
    float [ ] StandardizeInput ( float [ ] inputArray )
    {
        float [ ] standardized = new float [ inputArray. Length ];
        for ( int i = 0 ; i < inputArray. Length ; i++ )
        {
            int featureIdx = i % numFeatures;
            standardized [ i ] = ( inputArray [ i ] - means [ featureIdx ] ) / stdDevs [ featureIdx ];
        }
        return standardized;
    }


    void PredictSuccess ( )
    {
        if ( collectedIMUData. Count < windowSize )
        {
            Debug. LogWarning ( "데이터가 충분하지 않습니다." );
            return;
        }

        List<float [ ]> interpolatedData = InterpolateToTargetLength ( collectedIMUData , targetLength );
        int totalFrames = interpolatedData. Count;
        int stepSize = 5;
        int numPredictions = ( totalFrames - windowSize + 1 + stepSize - 1 ) / stepSize;

        List<int> predictions = new List<int> ( );

        for ( int startIdx = 0 ; startIdx < numPredictions * stepSize ; startIdx += stepSize )
        {
            float [ ] inputArray = new float [ windowSize * numFeatures ];
            int index = 0;

            for ( int i = startIdx ; i < startIdx + windowSize ; i++ )
            {
                for ( int j = 0 ; j < numFeatures ; j++ )  // ✅ 0부터 7개
                    inputArray [ index++ ] = interpolatedData [ i ] [ j ];
            }

            // 표준화 적용
            inputArray = StandardizeInput ( inputArray );

            using ( var tensor = new Tensor<float> ( new TensorShape ( 1 , windowSize , numFeatures ) , inputArray ) )
            {
                worker. Schedule ( tensor );

                using ( var outputTensor = worker. PeekOutput ( "output_layer" ) as Tensor<float> )
                {
                    outputTensor. ReadbackRequest ( );
                    outputTensor. ReadbackAndClone ( );

                    int numClasses = outputTensor. shape [ 1 ];
                    float maxVal = float. MinValue;
                    int predictedClass = -1;

                    for ( int c = 0 ; c < numClasses ; c++ )
                    {
                        float val = outputTensor [ 0 , c ];
                        if ( val > maxVal )
                        {
                            maxVal = val;
                            predictedClass = c;
                        }
                    }

                    predictions. Add ( predictedClass );
                }
            }
        }

        // 다중분류 예측 결과 처리
        ProcessPredictions ( predictions );
    }

    // ✅ 다중분류 결과 처리 함수
    void ProcessPredictions ( List<int> predictions )
    {
        int totalCount = predictions. Count;
        int [ ] classCounts = new int [ 5 ]; // 클래스 수에 맞게 조절

        foreach ( var p in predictions )
        {
            classCounts [ p ]++;
        }

        int majorityClass = classCounts. ToList ( ). IndexOf ( classCounts. Max ( ) );

        Debug. Log ( $"총 예측 구간: {totalCount}" );
        //Debug. Log ( $"클래스별 카운트: 0:{classCounts [ 0 ]}, 1:{classCounts [ 1 ]}, 2:{classCounts [ 2 ]}" );
        //Debug. Log ( $"클래스별 카운트: 0:{classCounts [ 0 ]}, 1:{classCounts [ 1 ]}, 2:{classCounts [ 2 ]}, 3:{classCounts [ 3 ]}" );
        Debug. Log ( $"클래스별 카운트: 0:{classCounts [ 0 ]}, 1:{classCounts [ 1 ]}, 2:{classCounts [ 2 ]}, 3:{classCounts [ 3 ]}, 4:{classCounts [ 4 ]}" );


        // 클래스별 확률(%) 출력
        for ( int i = 0 ; i < classCounts. Length ; i++ )
        {
            float percentage = ( float ) classCounts [ i ] / totalCount * 100f;
            Debug. Log ( $"클래스 {i} 확률: {percentage:F2}%" );
        }

        // 평균 확률도 예시로 출력 (여긴 majority class의 확률만 표시)
        float majorityPercentage = ( float ) classCounts [ majorityClass ] / totalCount * 100f;



        // ✅ Majority class별 메시지 출력
        switch ( majorityClass )
        {

            case 0:
                Debug. Log ( "무지개 그리기 동작 성공입니다." );
                if ( resultText != null )
                {
                    resultText. text = "무지개 그리기 동작 성공입니다.";
                    resultText. color = new Color ( 0.0f , 0.5f , 0.0f );
                    resultText. gameObject. SetActive ( true );
                }
                if ( averagePredictionText != null )
                {
                    averagePredictionText. text = $"성공 확률: {majorityPercentage:F2}%";
                    averagePredictionText. gameObject. SetActive ( true );
                }
                break;
            case 1:
                Debug. Log ( "실패 1: 움직이는 팔의 각도가 부족합니다." );
                if ( resultText != null )
                {
                    resultText. text = "움직이는 팔의 각도가 부족합니다.";
                    resultText. color = Color. red;
                    resultText. gameObject. SetActive ( true );
                }
                if ( averagePredictionText != null )
                {
                    averagePredictionText. text = $"실패 확률: {majorityPercentage:F2}%";
                    averagePredictionText. gameObject. SetActive ( true );
                }
                break;
            case 2:
                Debug. Log ( "실패 2: 팔꿈치가 구부러졌습니다." );
                if ( resultText != null )
                {
                    resultText. text = "팔꿈치가 구부러졌습니다.";
                    resultText. color = Color. red;
                    resultText. gameObject. SetActive ( true );
                }
                if ( averagePredictionText != null )
                {
                    averagePredictionText. text = $"실패 확률: {majorityPercentage:F2}%";
                    averagePredictionText. gameObject. SetActive ( true );
                }
                break;
            case 3:
                Debug. Log ( "실패 3: 팔의 위치가 앞으로 나가 있습니다." );
                if ( resultText != null )
                {
                    resultText. text = "팔의 위치가 앞으로 나가 있습니다.";
                    resultText. color = Color. red;
                    resultText. gameObject. SetActive ( true );
                }
                if ( averagePredictionText != null )
                {
                    averagePredictionText. text = $"실패 확률: {majorityPercentage:F2}%";
                    averagePredictionText. gameObject. SetActive ( true );
                }
                break;
            case 4:
                Debug. Log ( "실패 4: 팔이 회전되었습니다." );
                if ( resultText != null )
                {
                    resultText. text = "팔이 회전되었습니다.";
                    resultText. color = Color. red;
                    resultText. gameObject. SetActive ( true );
                }
                if ( averagePredictionText != null )
                {
                    averagePredictionText. text = $"실패 확률: {majorityPercentage:F2}%";
                    averagePredictionText. gameObject. SetActive ( true );
                }
                break;
            default:
                Debug. Log ( "알 수 없는 클래스입니다." );
                break;
        }
    }

    // 터치 버튼이 눌렸는지 확인
    public bool TouchButtonPress ( )
    {
        return hapticDll. TouchButtonPress ( );
    }

    public bool TouchButtonRelease ( )
    {
        return BstickManager. Instance. TouchButtonRelease ( );
    }

    void IMU_DataReceive ( )

    {
        BstickManager. Instance. IMU_DataReceive ( );

    }

    void OnDestroy ( )
    {
        worker. Dispose ( );
    }

}
