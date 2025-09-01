using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class IMUConvert : MonoBehaviour
{
    public string rawFolderPath;      // 원본 데이터 폴더 경로
    public string outputFolderPath;   // 변환된 데이터 저장 경로



    Vector3 acceleration_IMU = Vector3.zero;
    Vector3 acceleration1 = Vector3.zero;
    Vector3 acceleration2 = Vector3.zero;

    Vector3 velocity1 = Vector3.zero;   // Previous 1
    Vector3 velocity2 = Vector3.zero;   // Previous 1

    Vector3 imuGravity = new Vector3(0.0f, 1.0f, 0.0f);
    Vector3 worldGravity = Vector3.zero;

    Vector3 eulerCurrent;
    Vector3 eulerPrevious;       

    Quaternion quatBase;
    Quaternion quatIMU;
    Quaternion quatCurrent;
    Quaternion quatPrevious;

    float currTime = 0.0f;
    float prevTime = 0.0f;

    public float interval = 10.0f; 
    private float timer = 0f;

    string csvFileName;
    string inputFileName;
    string outputFileName;

    string folderPath = "D:/IMUData/Drum_direction_RawData";

    string FileName1 = "Front.csv";
    string FileName2 = "Left.csv";
    string FileName3 = "Back.csv";
    string FileName4 = "Right.csv";

    string FileName1_New = "Front_new.csv";
    string FileName2_New = "Left_new.csv";
    string FileName3_New = "Back_new.csv";
    string FileName4_New = "Right_new.csv";

    public float alpha = 0.6f; // EWMA 필터 계수(0~1), 높을수록 원본 비중 ↑
    public float spikeThreshold = 20f; // 급변 감지 임계값(deg)

    void Start()    
    {
        if ( !Directory. Exists ( rawFolderPath ) )
        {
            Debug. LogError ( "Raw 데이터 폴더 경로가 올바르지 않습니다: " + rawFolderPath );
            return;
        }

        if ( !Directory. Exists ( outputFolderPath ) )
        {
            Directory. CreateDirectory ( outputFolderPath );
        }

        string [ ] csvFiles = Directory. GetFiles ( rawFolderPath , "*.csv" );

        foreach ( string inputFile in csvFiles )
        {
            string outputFile = Path. Combine ( outputFolderPath , Path. GetFileNameWithoutExtension ( inputFile ) + "_new.csv" );

            ResetData ( );
            ProcessCsv ( inputFile , outputFile );
        }

        Debug. Log ( "모든 CSV 파일 처리 완료!" );
    }

    void ResetData()
    {
        acceleration1 = Vector3.zero;
        acceleration2 = Vector3.zero;

        velocity1 = Vector3.zero;   // Previous 1
        velocity2 = Vector3.zero;   // Previous 1

        quatBase = Quaternion.identity;
        quatCurrent = Quaternion.identity;
        quatPrevious = Quaternion.identity;
        eulerPrevious = Vector3.zero;
        eulerCurrent = Vector3.zero;

        currTime = 0.0f;
        prevTime = 0.0f;
    }

    void ProcessCsv(string inputFileName, string outputFileName)
    {
        if ( !File. Exists ( inputFileName ) )
        {
            Debug. LogWarning ( "파일이 존재하지 않습니다: " + inputFileName );
            return;
        }

        string [ ] lines = File. ReadAllLines ( inputFileName );
        if ( lines. Length < 2 )
        {
            Debug. LogWarning ( inputFileName + " 파일에 데이터 없음." );
            return;
        }

        //string header = lines[0] + ",Acc_NX,Acc_NY,Acc_NZ,Vel_NX,Vel_NY,Vel_NZ,Pos_X,Pos_Y,Pos_Z,Dist_X,Dist_y,Dist_z, Rot_N1, Rot_N2, Rot_N3";
        //string header = lines[0] + ",Acc_NX,Acc_NY,Acc_NZ,Vel_NX,Vel_NY,Vel_NZ,Pos_X,Pos_Y,Pos_Z,Dist_X,Dist_y,Dist_z, Quat_x, Quat_y, Quat_z,  Quat_w";
        string header = lines[0] + ",Acc_NX,Acc_NY,Acc_NZ,Vel_NX,Vel_NY,Vel_NZ,Pos_X,Pos_Y,Pos_Z,Dist_X,Dist_y,Dist_z,Euler_x,Euler_y,Euler_z";  // Euler 데이터 활용

        List<string> outputLines = new List<string> { header };

        Vector3 position = Vector3.zero;
        Vector3 cumulativeDistance = Vector3.zero;

        float deltaTime = 0.0f;

        // 1. First Line Parsing
        string[] values = lines[1].Split(',');
        currTime = float.Parse(values[0]);

        // Save First Acceleration Data
        //acceleration1 = new Vector3(float.Parse(values[1]), float.Parse(values[2]), float.Parse(values[3]));
        acceleration_IMU = new Vector3(-float.Parse(values[2]), float.Parse(values[1]), -float.Parse(values[3]));
        //acceleration1 = acceleration_IMU;

        quatIMU = Canonicalize(float.Parse(values[8]), float.Parse(values[9]), float.Parse(values[10]), float.Parse(values[7]));
        quatIMU = ConvertIMUToUnity(quatIMU);
        quatBase = Quaternion.Inverse(quatIMU);
        
        ChangeDataAfterCalculate();

        // 2. Second Line Parsing
        values = lines[2].Split(',');
        currTime = float.Parse(values[0]);
        deltaTime = currTime - prevTime;

        //acceleration1 = new Vector3(float.Parse(values[1]) + 1.0f, float.Parse(values[2]), float.Parse(values[3]));
        acceleration_IMU = new Vector3(-float.Parse(values[2]), float.Parse(values[1]), -float.Parse(values[3]));
        acceleration1 = acceleration_IMU;

        velocity1 += (acceleration1 * deltaTime);

        ChangeDataAfterCalculate();
        Debug.Log("Line Length" + lines.Length);

        for (int i = 3; i < lines.Length; i++)
        {           
             values = lines[i].Split(',');

            // Calculate Delta Time
            currTime = float.Parse(values[0]);
            deltaTime = currTime - prevTime;

            // Quaternion Correction            
            quatIMU = Canonicalize(float.Parse(values[8]), float.Parse(values[9]), float.Parse(values[10]), float.Parse(values[7]));
            quatIMU = ConvertIMUToUnity(quatIMU);

            quatCurrent = quatBase * quatIMU;

            eulerCurrent = quatCurrent.eulerAngles;
            eulerCurrent.x = Mathf.DeltaAngle(0f, eulerCurrent.x);
            eulerCurrent.y = Mathf.DeltaAngle(0f, eulerCurrent.y);
            eulerCurrent.z = Mathf.DeltaAngle(0f, eulerCurrent.z);

            /*
            // 5. 스파이크 제거
            eulerCurrent = RemoveSpikes(eulerCurrent, eulerPrevious, spikeThreshold);

            // 6. EWMA 필터 적용
            Vector3 filtered = new Vector3(
                alpha * eulerCurrent.x + (1 - alpha) * eulerPrevious.x,
                alpha * eulerCurrent.y + (1 - alpha) * eulerPrevious.y,
                alpha * eulerCurrent.z + (1 - alpha) * eulerPrevious.z
            );
            */

            //eulerCurrent = filtered;

            //Vector3 rotVector = ToRotVec(quatCurrent);


            //Vector3 rotCurrent = rotVector / Mathf.PI;

            /*
              if (Quaternion.Dot(quatPrevious, quatCurrent) < 0)
              {
                  quatCurrent.Set(-quatCurrent.x, -quatCurrent.y, -quatCurrent.z, -quatCurrent.w);
              }
            */


            // Gravity Decomposition
            //Vector3 worldGravity = quatIMU * imuGravity;            


            //Vector3 acceleration_IMU = new Vector3(float.Parse(values[1]), float.Parse(values[2]), float.Parse(values[3]));
            Vector3 acceleration_IMU = new Vector3(-float.Parse(values[2]), float.Parse(values[1]), -float.Parse(values[3]));
            //acceleration1 = new Vector3(-float.Parse(values[2]), float.Parse(values[1]), -float.Parse(values[3]));
            //Debug.Log(acceleration_IMU);
            //Debug.Log(worldGravity);

            acceleration1 = acceleration_IMU - worldGravity;
            //Debug.Log(acceleration1);

            velocity1 += (acceleration2 * deltaTime);
            Vector3 delta_s = velocity2 * deltaTime;
            position += delta_s;

            Vector3 delta_s_abs = new Vector3(Mathf.Abs(delta_s.x), Mathf.Abs(delta_s.y), Mathf.Abs(delta_s.z));
            cumulativeDistance = cumulativeDistance + delta_s_abs;

            // 새로운 라인 작성
            string newLine = lines[i] + $",{acceleration1.x},{acceleration1.y},{acceleration1.z},{velocity1.x},{velocity1.y},{velocity1.z},{position.x},{position.y},{position.z},{cumulativeDistance.x},{cumulativeDistance.y},{cumulativeDistance.z}, {eulerCurrent.x}, {eulerCurrent.y}, {eulerCurrent.z}";
            //string newLine = lines[i] + $",{acceleration1.x},{acceleration1.y},{acceleration1.z},{velocity1.x},{velocity1.y},{velocity1.z},{position.x},{position.y},{position.z},{cumulativeDistance.x},{cumulativeDistance.y},{cumulativeDistance.z}, {angleVelocity.x}, {angleVelocity.y}, {angleVelocity.z}";
            //string newLine = lines[i] + $",{acceleration1.x},{acceleration1.y},{acceleration1.z},{velocity1.x},{velocity1.y},{velocity1.z},{position.x},{position.y},{position.z},{cumulativeDistance.x},{cumulativeDistance.y},{cumulativeDistance.z}, {quatCurrent.x}, {quatCurrent.y}, {quatCurrent.z}, {quatCurrent.w}";
            //string newLine = lines[i] + $",{acceleration1.x},{acceleration1.y},{acceleration1.z},{velocity1.x},{velocity1.y},{velocity1.z},{position.x},{position.y},{position.z},{cumulativeDistance.x},{cumulativeDistance.y},{cumulativeDistance.z}, {rotCurrent.x}, {rotCurrent.y}, {rotCurrent.z}";
            outputLines.Add(newLine);

            ChangeDataAfterCalculate();
        }

        File. WriteAllLines ( outputFileName , outputLines. ToArray ( ) );
        Debug. Log ( "처리 완료: " + Path. GetFileName ( outputFileName ) );
    }

    void ChangeDataAfterCalculate()
    {     
        acceleration2 = acceleration1;
        velocity2 = velocity1;

        prevTime = currTime;
        quatPrevious = quatCurrent;
        eulerPrevious = eulerCurrent;
    }

    public Quaternion Canonicalize(float x, float y, float z, float w)
    {
        Quaternion quat = new Quaternion(x, y, z, w); // x, y, z, w 변환

        quat.Normalize();

        return quat;
    }

    public Quaternion Canonicalize(Quaternion quat)
    {
        quat.Normalize();

        if (Quaternion.Dot(quatPrevious, quat) < 0f)
        {
            quat = new Quaternion(-quat.x, -quat.y, -quat.z, -quat.w);
        }

        return quat;        
    }

    public Quaternion ConvertIMUToUnity(Quaternion imuQ)
    {
        Quaternion qLeftHand = new Quaternion(imuQ.x, imuQ.y, -imuQ.z, -imuQ.w);

        return new Quaternion(-qLeftHand.y, qLeftHand.x, qLeftHand.z, qLeftHand.w);
                      
        //Quaternion sensorToUnity = Quaternion.Euler(90f, 0f, 0f);
        //imuQ = sensorToUnity * imuQ;        
        //return imuQ;
    }

    // 스파이크 제거 함수
    private Vector3 RemoveSpikes(Vector3 current, Vector3 previous, float threshold)
    {
        Vector3 result = current;

        if (Mathf.Abs(current.x - previous.x) > threshold) result.x = previous.x;
        if (Mathf.Abs(current.y - previous.y) > threshold) result.y = previous.y;
        if (Mathf.Abs(current.z - previous.z) > threshold) result.z = previous.z;

        return result;
    }
}

