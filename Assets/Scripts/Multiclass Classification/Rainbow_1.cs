using UnityEngine;
using Unity. Sentis;
using System. Collections. Generic;
using HapticDllCs;
using System. IO;
using System. Linq;
using TMPro;
using UnityEngine. UIElements;
using Unity. VisualScripting;

public class Rainbow_1 : MonoBehaviour
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
    private Vector3 acceleration1 = Vector3. zero;
    private Vector3 acceleration2 = Vector3. zero;
    private Vector3 velocity1 = Vector3. zero;
    private Vector3 velocity2 = Vector3. zero;
    private Vector3 cumulativeDistance = Vector3. zero;
    private Quaternion quatCurrent = Quaternion. identity;
    private bool isFirstFrame = true;
    private List<float> deltaTimes = new List<float> ( ); // 디버깅용 deltaTime 저장
    private Quaternion quatBase = Quaternion. identity;
    private bool isFirstQuatFrame = true;
    private Quaternion quatPrevious = Quaternion. identity;
    private float startTime;
    private float lastTime;

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
    private const int numFeatures = 14; //학습에 사용된 피처의 개수 = Timestamp, Acc x, y, z, Gyro x, y, z



    //private readonly float [ ] means = new float [ numFeatures ] { 0.14093758f , -0.31939248f , -0.20478589f , -5.69522844f , 9.4695212f , -23.49603458f , 0.22221715f , -0.35618472f , 0.10989852f , 0.58091606f , 4.92664178f , -1.86100296f , -0.61094891f };
    //private readonly float [ ] stdDevs = new float [ numFeatures ] { 0.78793996f , 0.38605152f , 0.29031338f , 17.5913785f , 34.29226535f , 35.17691902f , 0.21315536f , 0.32320883f , 0.15637378f , 0.34360513f , 9.35913106f , 3.16053342f , 2.21105564f };

    private readonly float [ ] means = new float [ numFeatures ] { 3.00482305f , -0.04797067f , -0.37401806f , -0.24269796f , -8.48914332f , 3.26042065f , -18.70910336f , 0.24356058f , -0.22081938f , 0.15437178f , 0.63349741f , 4.45010855f , -2.05248692f , -0.8331573f };
    private readonly float [ ] stdDevs = new float [ numFeatures ] { 2.15183072f , 0.77411677f , 0.40380064f , 0.38371043f , 91.44340974f , 139.95903291f , 81.21082703f , 0.23824852f , 0.31365024f , 0.17752747f , 0.32123949f , 9.44930372f , 3.42487732f , 2.6840892f };


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

        //비스틱 버튼 컨트롤용
        if ( BstickManager. Instance. TouchButtonRelease ( ) )
        {
            ToggleMeasurement ( );
        }
        if ( isMeasuring )
        {
            CollectIMUData ( );

        }

        //스페이스바 컨트롤용
        //if ( BstickManager. Instance. isTracking )
        //{
        //    if ( !isMeasuring )
        //    {
        //        ToggleMeasurement ( );  // 측정 시작
        //    }

        //    CollectIMUData ( );


        //}
        //else
        //{
        //    if ( isMeasuring )
        //    {
        //        ToggleMeasurement ( );  // 측정 종료
        //    }
        //}
    }

    void ResetData ( )
    {
        acceleration1 = Vector3. zero;
        acceleration2 = Vector3. zero;

        velocity1 = Vector3. zero;   // Previous 1
        velocity2 = Vector3. zero;   // Previous 1

        position = Vector3. zero;
        cumulativeDistance = Vector3. zero;

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
            SaveFullIMUDataToCSV ( );
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

        if ( accelList == null || gyroList == null || accelList. Count < 3 || gyroList. Count < 3 || quatList == null || quatList. Count < 4 )
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

        // 가속도 (X+1 보정 IMUConvert 동일)
        acceleration1 = new Vector3 ( accelList [ 0 ] + 1.0f , accelList [ 1 ] , accelList [ 2 ] );

        // 쿼터니언
        Quaternion quat1 = new Quaternion ( quatList [ 1 ] , quatList [ 2 ] , quatList [ 3 ] , quatList [ 0 ] );

        // 첫 프레임 때 기준 설정
        if ( isFirstQuatFrame )
        {
            quatBase = Quaternion. Inverse ( quat1 );
            isFirstQuatFrame = false;
        }

        quatCurrent = quat1 * quatBase;

        // 쿼터니언 튐 방지 (Dot Product)
        if ( Quaternion. Dot ( quatPrevious , quatCurrent ) < 0 )
        {
            quatCurrent = new Quaternion ( -quatCurrent. x , -quatCurrent. y , -quatCurrent. z , -quatCurrent. w );
        }

        // 속도 누적
        velocity1 += acceleration2 * deltaTime;

        // 위치 변화량
        Vector3 delta_s = velocity2 * deltaTime;
        position += delta_s;

        // 거리 변화량 (절대값 누적)
        Vector3 delta_s_abs = new Vector3 ( Mathf. Abs ( delta_s. x ) , Mathf. Abs ( delta_s. y ) , Mathf. Abs ( delta_s. z ) );
        cumulativeDistance += delta_s_abs;

        // 데이터 저장
        float [ ] imuFrame = {
            currentTime,
            accelList[0], accelList[1], accelList[2],
            gyroList[0], gyroList[1], gyroList[2],
            quatCurrent.x, quatCurrent.y, quatCurrent.z, quatCurrent.w,
            position.x, position.y, position.z,
         };


        imuDataList. Add ( imuFrame );
        collectedIMUData. Add ( imuFrame );

        acceleration2 = acceleration1;
        velocity2 = velocity1;
        quatPrevious = quatCurrent;
        lastTime = currentTime;
    }


    void SaveFullIMUDataToCSV ( )
    {
        try
        {
            if ( imuDataList. Count == 0 )
            {
                Debug. LogError ( "imuDataList가 비어 있습니다." );
                return;
            }

            string fileName = Path. Combine ( saveFilePath , "RainbowPredictionData_" + fileCount + ".csv" );
            fileCount++;

            using ( StreamWriter writer = new StreamWriter ( fileName , false ) )
            {
                // 헤더 작성
                writer. WriteLine ( "Timestamp,Acc_X,Acc_Y,Acc_Z,Gyro_X,Gyro_Y,Gyro_Z,Pos_X,Pos_Y,Pos_Z,Dist_X,Dist_Y,Dist_Z,Quat_X,Quat_Y,Quat_Z" );

                for ( int i = 0 ; i < imuDataList. Count ; i++ )
                {
                    float [ ] data = imuDataList [ i ];
                    writer. WriteLine ( $"{data [ 0 ]},{data [ 1 ]},{data [ 2 ]},{data [ 3 ]}," +
                                     $"{data [ 4 ]},{data [ 5 ]},{data [ 6 ]}," +
                                     $"{data [ 7 ]},{data [ 8 ]},{data [ 9 ]}," +
                                     $"{data [ 10 ]},{data [ 11 ]},{data [ 12 ]}," +
                                     $"{data [ 13 ]},{data [ 14 ]},{data [ 15 ]}, {data [ 16 ]}" );
                }
            }

            Debug. Log ( $"IMU 데이터 CSV 저장 완료: {fileName}" );
        }
        catch ( System. Exception e )
        {
            Debug. LogError ( $"IMU 데이터 CSV 저장 실패: {e. Message}" );
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
            float interpolatedPosX = Mathf. Lerp ( dataList [ index1 ] [ 11 ] , dataList [ index2 ] [ 11 ] , t );
            float interpolatedPosY = Mathf. Lerp ( dataList [ index1 ] [ 12 ] , dataList [ index2 ] [ 12 ] , t );
            float interpolatedPosZ = Mathf. Lerp ( dataList [ index1 ] [ 13 ] , dataList [ index2 ] [ 13 ] , t );

            // 가속도와 자이로스코프 데이터를 보간
            //float interpolatedAccelX = Mathf. Lerp ( dataList [ index1 ] [ 0 ] , dataList [ index2 ] [ 0 ] , t );
            //float interpolatedAccelY = Mathf. Lerp ( dataList [ index1 ] [ 1 ] , dataList [ index2 ] [ 1 ] , t );
            //float interpolatedAccelZ = Mathf. Lerp ( dataList [ index1 ] [ 2 ] , dataList [ index2 ] [ 2 ] , t );
            //float interpolatedGyroX = Mathf. Lerp ( dataList [ index1 ] [ 3 ] , dataList [ index2 ] [ 3 ] , t );
            //float interpolatedGyroY = Mathf. Lerp ( dataList [ index1 ] [ 4 ] , dataList [ index2 ] [ 4 ] , t );
            //float interpolatedGyroZ = Mathf. Lerp ( dataList [ index1 ] [ 5 ] , dataList [ index2 ] [ 5 ] , t );
            //float interpolatedQuatX = Mathf. Lerp ( dataList [ index1 ] [ 6 ] , dataList [ index2 ] [ 6 ] , t );
            //float interpolatedQuatY = Mathf. Lerp ( dataList [ index1 ] [ 7 ] , dataList [ index2 ] [ 7 ] , t );
            //float interpolatedQuatZ = Mathf. Lerp ( dataList [ index1 ] [ 8 ] , dataList [ index2 ] [ 8 ] , t );
            //float interpolatedQuatW = Mathf. Lerp ( dataList [ index1 ] [ 9 ] , dataList [ index2 ] [ 9 ] , t );
            //float interpolatedPosX = Mathf. Lerp ( dataList [ index1 ] [ 10 ] , dataList [ index2 ] [ 10 ] , t );
            //float interpolatedPosY = Mathf. Lerp ( dataList [ index1 ] [ 11 ] , dataList [ index2 ] [ 11 ] , t );
            //float interpolatedPosZ = Mathf. Lerp ( dataList [ index1 ] [ 12 ] , dataList [ index2 ] [ 12 ] , t );


            // 보간된 값을 새로운 프레임에 추가
            //float [ ] interpolatedFrame = new float [ 13 ];
            //interpolatedFrame [ 0 ] = interpolatedAccelX;
            //interpolatedFrame [ 1 ] = interpolatedAccelY;
            //interpolatedFrame [ 2 ] = interpolatedAccelZ;
            //interpolatedFrame [ 3 ] = interpolatedGyroX;
            //interpolatedFrame [ 4 ] = interpolatedGyroY;
            //interpolatedFrame [ 5 ] = interpolatedGyroZ;
            //interpolatedFrame [ 6 ] = interpolatedQuatX;
            //interpolatedFrame [ 7 ] = interpolatedQuatY;
            //interpolatedFrame [ 8 ] = interpolatedQuatZ;
            //interpolatedFrame [ 9 ] = interpolatedQuatW;
            //interpolatedFrame [ 10 ] = interpolatedPosX;
            //interpolatedFrame [ 11 ] = interpolatedPosY;
            //interpolatedFrame [ 12 ] = interpolatedPosZ;

            float [ ] interpolatedFrame = new float [ 14 ];
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
            interpolatedFrame [ 11 ] = interpolatedPosX;
            interpolatedFrame [ 12 ] = interpolatedPosY;
            interpolatedFrame [ 13 ] = interpolatedPosZ;


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
        int [ ] classCounts = new int [ 3 ]; // 클래스 수에 맞게 조절

        foreach ( var p in predictions )
        {
            classCounts [ p ]++;
        }

        int majorityClass = classCounts. ToList ( ). IndexOf ( classCounts. Max ( ) );

        Debug. Log ( $"총 예측 구간: {totalCount}" );
        Debug. Log ( $"클래스별 카운트: 0:{classCounts [ 0 ]}, 1:{classCounts [ 1 ]}, 2:{classCounts [ 2 ]}" );
        //Debug. Log ( $"클래스별 카운트: 0:{classCounts [ 0 ]}, 1:{classCounts [ 1 ]}, 2:{classCounts [ 2 ]}, 3:{classCounts [ 3 ]}" );
        //Debug. Log ( $"클래스별 카운트: 0:{classCounts [ 0 ]}, 1:{classCounts [ 1 ]}, 2:{classCounts [ 2 ]}, 3:{classCounts [ 3 ]}, 4:{classCounts [ 4 ]}" );


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
                    resultText. text = "무지개 그리기 동작 실패입니다.";
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
                Debug. Log ( "실패 2: 팔이 회전되었습니다." );
                if ( resultText != null )
                {
                    resultText. text = "무지개 그리기 동작 실패입니다.";
                    resultText. color = Color. red;
                    resultText. gameObject. SetActive ( true );
                }
                if ( averagePredictionText != null )
                {
                    averagePredictionText. text = $"실패 확률: {majorityPercentage:F2}%";
                    averagePredictionText. gameObject. SetActive ( true );
                }
                break;
            //case 3:
            //    Debug. Log ( "실패 3: 팔의 위치가 앞으로 나가 있습니다." );
            //    if ( resultText != null )
            //    {
            //        resultText. text = "무지개 그리기 동작 실패입니다.";
            //        resultText. color = Color. red;
            //        resultText. gameObject. SetActive ( true );
            //    }
            //    if ( averagePredictionText != null )
            //    {
            //        averagePredictionText. text = $"실패 확률: {majorityPercentage:F2}%";
            //        averagePredictionText. gameObject. SetActive ( true );
            //    }
            //    break;
            //case 4:
            //    Debug. Log ( "실패 4: 팔이 회전되었습니다." );
            //    if ( resultText != null )
            //    {
            //        resultText. text = "무지개 그리기 동작 실패입니다.";
            //        resultText. color = Color. red;
            //        resultText. gameObject. SetActive ( true );
            //    }
            //    if ( averagePredictionText != null )
            //    {
            //        averagePredictionText. text = $"실패 확률: {majorityPercentage:F2}%";
            //        averagePredictionText. gameObject. SetActive ( true );
            //    }
            //    break;
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

