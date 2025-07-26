using UnityEngine;
using Unity. Sentis;
using System. Collections. Generic;
using HapticDllCs;
using System. IO;
using System. Linq;
using TMPro;

public class Dumbbell_space : MonoBehaviour
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


    [Header ( "CSV" )]
    public string saveFilePath = "C:\\Users\\HARAM\\Desktop\\BsitckData\\Rainbow_Interpol";
    private bool isMeasuring = false; // 데이터 측정 여부
    private int fileCount = 1; // 파일 이름에 사용할 카운터

    [Header ( "AI" )]
    public ModelAsset dumbbellPredictionModel;  // ONNX 모델
    Worker worker;
    Tensor<float> tensor;
    private const int windowSize = 20;  // LSTM 입력 시퀀스 길이 (10 프레임)
    private const int targetLength = 241; // 학습에 사용한 보간 데이터 길이
    private const int numFeatures = 11;

    private readonly float [ ] means = new float [ numFeatures ] { 0.98418445f,  -0.22333079f,  -0.07803041f,   0.42631591f, -70.77228552f,
  16.77271984f,   7.71771155f,   0.08334781f,   0.23820423f,   0.09726064f,   0.64298649f, };
    private readonly float [ ] stdDevs = new float [ numFeatures ] { 0.5837098f,   0.31507044f,  0.61829968f,  0.55283349f, 59.34829778f, 34.40204681f,
 34.92500387f,  0.21142224f,  0.19059224f, 0.18739241f,  0.20494981f, };



    void Start ( )
    {
        // Sentis ONNX 모델 로드
        var runtimeModel = ModelLoader. Load ( dumbbellPredictionModel );
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

    void FixedUpdate ( )
    {
        if ( BstickManager. Instance == null )
        {
            Debug. LogError ( "BstickManager.Instance is null!" );
            return;
        }

        //비스틱 버튼 컨트롤용
        //if ( BstickManager. Instance. TouchButtonRelease ( ) )
        //{
        //    ToggleMeasurement ( );
        //}
        //if ( isMeasuring )
        //{
        //    CollectIMUData ( );
        //}

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

    void ToggleMeasurement ( )
    {
        if ( !isMeasuring )
        {
            startTime = Time. time;
            velocity = Vector3. zero;
            position = Vector3. zero;
            lastTime = 0f;
            isFirstFrame = true;
            isFirstQuatFrame = true;
            deltaTimes. Clear ( );
            isMeasuring = true;
            collectedIMUData. Clear ( );
            imuDataList. Clear ( );

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
            SaveRawDataToCSV ( ); // 위치값 계산 후 원시 데이터 저장
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
        Quaternion quat1 = new Quaternion ( quatList [ 1 ] , quatList [ 2 ] , quatList [ 3 ] , quatList [ 0 ] );

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

        Vector3 acceleration = new Vector3 ( accelList [ 0 ] , accelList [ 1 ] , accelList [ 2 ] );
        velocity += acceleration * deltaTime;
        position += velocity * deltaTime;

        // 속도의 절댓값 × dt로 거리 계산
        float dx = Mathf. Abs ( velocity. x ) * deltaTime;
        float dy = Mathf. Abs ( velocity. y ) * deltaTime;
        float dz = Mathf. Abs ( velocity. z ) * deltaTime;

        if ( isFirstQuatFrame )
        {
            startQuat = quat1;
            quatBase = Quaternion. Inverse ( startQuat );
            isFirstQuatFrame = false;
        }

        Quaternion quat = quat1 * quatBase;

        float [ ] imuFrame = {
            currentTime,
            accelList[0], accelList[1], accelList[2],
            gyroList[0], gyroList[1], gyroList[2],
            quat.x, quat.y, quat.z, quat.w,
            //position. x, position.y, position.z,
            //cumulativeDx, cumulativeDy, cumulativeDz

        };


        imuDataList. Add ( imuFrame );
        collectedIMUData. Add ( imuFrame );
        lastTime = currentTime;
        lastPosition = position;
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
            string baseFileName = "Dumbbell_prediction_data";
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
                writer. WriteLine ( "Timestamp,Acc_X,Acc_Y,Acc_Z,Gyro_X,Gyro_Y,Gyro_Z,Quat_X,Quat_Y,Quat_Z,Quat_W" );


                foreach ( float [ ] data in imuDataList )
                {
                    writer. WriteLine ( $"{data [ 0 ]},{data [ 1 ]},{data [ 2 ]},{data [ 3 ]},{data [ 4 ]},{data [ 5 ]},{data [ 6 ]},{data [ 7 ]},{data [ 8 ]},{data [ 9 ]},{data [ 10 ]}" );
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


            // 가속도와 자이로스코프 데이터를 보간
            float interpolatedTime = Mathf. Lerp ( dataList [ index1 ] [ 0 ] , dataList [ index2 ] [ 0 ] , t );
            float interpolatedAccelX = Mathf. Lerp ( dataList [ index1 ] [ 1 ] , dataList [ index2 ] [ 1 ] , t );
            float interpolatedAccelY = Mathf. Lerp ( dataList [ index1 ] [ 2 ] , dataList [ index2 ] [ 2 ] , t );
            float interpolatedAccelZ = Mathf. Lerp ( dataList [ index1 ] [ 3 ] , dataList [ index2 ] [ 3 ] , t );
            float interpolatedGyroX = Mathf. Lerp ( dataList [ index1 ] [ 4 ] , dataList [ index2 ] [ 4 ] , t );
            float interpolatedGyroY = Mathf. Lerp ( dataList [ index1 ] [ 5 ] , dataList [ index2 ] [ 5 ] , t );
            float interpolatedGyroZ = Mathf. Lerp ( dataList [ index1 ] [ 6 ] , dataList [ index2 ] [ 6 ] , t );
            float interpolatedQuatX = Mathf. Lerp ( dataList [ index1 ] [ 7 ] , dataList [ index2 ] [ 7 ] , t );
            float interpolatedQuatY = Mathf. Lerp ( dataList [ index1 ] [ 8 ] , dataList [ index2 ] [ 8 ] , t );
            float interpolatedQuatZ = Mathf. Lerp ( dataList [ index1 ] [ 9 ] , dataList [ index2 ] [ 9 ] , t );
            float interpolatedQuatW = Mathf. Lerp ( dataList [ index1 ] [ 10 ] , dataList [ index2 ] [ 10 ] , t );
            //float interpolatedPosX = Mathf. Lerp ( dataList [ index1 ] [ 7 ] , dataList [ index2 ] [ 7 ] , t );
            //float interpolatedPosY = Mathf. Lerp ( dataList [ index1 ] [ 8 ] , dataList [ index2 ] [ 8 ] , t );
            //float interpolatedPosZ = Mathf. Lerp ( dataList [ index1 ] [ 9 ] , dataList [ index2 ] [ 9 ] , t );
            //float interpolatedDistX = Mathf. Lerp ( dataList [ index1 ] [ 10 ] , dataList [ index2 ] [ 10 ] , t );
            //float interpolatedDistY = Mathf. Lerp ( dataList [ index1 ] [ 11 ] , dataList [ index2 ] [ 11 ] , t );
            //float interpolatedDistZ = Mathf. Lerp ( dataList [ index1 ] [ 12 ] , dataList [ index2 ] [ 12 ] , t );




            // 보간된 값을 새로운 프레임에 추가
            float [ ] interpolatedFrame = new float [ 11 ];
            interpolatedFrame [ 0 ] = interpolatedTime;
            interpolatedFrame [ 1 ] = interpolatedAccelX;
            interpolatedFrame [ 2 ] = interpolatedAccelY;
            interpolatedFrame [ 3 ] = interpolatedAccelZ;
            interpolatedFrame [ 4 ] = interpolatedGyroX;
            interpolatedFrame [ 5 ] = interpolatedGyroY;
            interpolatedFrame [ 6 ] = interpolatedGyroZ;
            interpolatedFrame [ 7 ] = interpolatedQuatX;
            interpolatedFrame [ 8 ] = interpolatedQuatY;
            interpolatedFrame [ 9 ] = interpolatedQuatZ;
            interpolatedFrame [ 10 ] = interpolatedQuatW;
            //interpolatedFrame [ 7 ] = interpolatedPosX;
            //interpolatedFrame [ 8 ] = interpolatedPosY;
            //interpolatedFrame [ 9 ] = interpolatedPosZ;
            //interpolatedFrame [ 10 ] = interpolatedDistX;
            //interpolatedFrame [ 11 ] = interpolatedDistY;
            //interpolatedFrame [ 12 ] = interpolatedDistZ;

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
        int stepSize = 10;
        int numPredictions = ( totalFrames - windowSize + 1 + stepSize - 1 ) / stepSize;

        List<int> predictions = new List<int> ( );

        for ( int startIdx = 0 ; startIdx < numPredictions * stepSize ; startIdx += stepSize )
        {
            float [ ] inputArray = new float [ windowSize * numFeatures ];
            int index = 0;

            for ( int i = startIdx ; i < startIdx + windowSize ; i++ )
            {
                for ( int j = 0 ; j < numFeatures ; j++ )
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

    //다중분류 결과 처리 함수
    void ProcessPredictions ( List<int> predictions )
    {
        int totalCount = predictions. Count;
        int [ ] classCounts = new int [ 4 ]; // 클래스 수에 맞게 조절

        foreach ( var p in predictions )
        {
            classCounts [ p ]++;
        }

        int majorityClass = classCounts. ToList ( ). IndexOf ( classCounts. Max ( ) );

        Debug. Log ( $"총 예측 구간: {totalCount}" );
        Debug. Log ( $"클래스별 카운트: 0:{classCounts [ 0 ]}, 1:{classCounts [ 1 ]}, 2:{classCounts [ 2 ]}, 3:{classCounts [ 3 ]}" );

        // 클래스별 확률(%) 출력
        for ( int i = 0 ; i < classCounts. Length ; i++ )
        {
            float percentage = ( float ) classCounts [ i ] / totalCount * 100f;
            Debug. Log ( $"클래스 {i} 확률: {percentage:F2}%" );
        }

        // 평균 확률
        float majorityPercentage = ( float ) classCounts [ majorityClass ] / totalCount * 100f;


        //Majority class별 메시지 출력
        switch ( majorityClass )
        {

            case 0:
                Debug. Log ( "아령 들기 동작 성공입니다." );
                if ( resultText != null )
                {
                    resultText. text = "아령 들기 동작 성공입니다.";
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
                Debug. Log ( "실패 1: 팔꿈치와 팔목 사이의 각도가 정상 범위보다 부족합니다." );
                if ( resultText != null )
                {
                    resultText. text = "아령 들기 동작 실패입니다.";
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
                Debug. Log ( "실패 2: 팔의 위치가 왼쪽으로 이동되어 정상 범위를 벗어났습니다." );
                if ( resultText != null )
                {
                    resultText. text = "아령 들기 동작 실패입니다.";
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
                Debug. Log ( "실패 3: 팔의 위치가 오른쪽으로 이동되어 정상 범위를 벗어났습니다." );
                if ( resultText != null )
                {
                    resultText. text = "아령 들기 동작 실패입니다.";
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


    void SaveDataToCSV ( )
    {
        try
        {
            if ( imuDataList. Count == 0 )
            {
                Debug. LogError ( "imuDataList가 비어 있습니다." );
                return;
            }

            // 보간 수행
            List<float [ ]> interpolatedData = InterpolateToTargetLength ( imuDataList , targetLength );

            if ( interpolatedData. Count == 0 )
            {
                Debug. LogError ( "보간된 데이터가 비어 있습니다." );
                return;
            }
            foreach ( var data in interpolatedData )
            {
                if ( data. Length < 7 )
                {
                    Debug. LogError ( $"잘못된 데이터 크기: {data. Length} (7이어야 함)" );
                    return;
                }
            }

            string fileName = Path. Combine ( saveFilePath , "data_" + fileCount + ".csv" );
            fileCount++;

            using ( StreamWriter writer = new StreamWriter ( fileName , false ) )
            {
                writer. WriteLine ( "TimeStamp,Accel_X,Accel_Y,Accel_Z,Gyro_X,Gyro_Y,Gyro_Z" );
                foreach ( float [ ] data in interpolatedData ) // 보간된 데이터 저장
                {
                    writer. WriteLine ( $"{data [ 0 ]},{data [ 1 ]},{data [ 2 ]},{data [ 3 ]},{data [ 4 ]},{data [ 5 ]},{data [ 6 ]}" );
                }
            }
            Debug. Log ( $"보간 데이터 저장 완료: {fileName}" );
        }
        catch ( System. Exception e )
        {
            Debug. LogError ( $"보간 데이터 저장 실패: {e. Message}" );
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
