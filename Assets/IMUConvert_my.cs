using System.Collections.Generic;
using System.IO;
using UnityEngine;


public class IMUConvert_my : MonoBehaviour
{
    public string rawDataFolderPath = "D:/IMUData"; //원본 데이터 폴더
    public string convertDataFolderPath = "D:/IMUData/Converted"; //컨버팅 후 데이터 저장할 폴더

    Vector3 acceleration1 = Vector3.zero;
    Vector3 acceleration2 = Vector3.zero;

    Vector3 velocity1 = Vector3.zero;   // Previous 1
    Vector3 velocity2 = Vector3.zero;   // Previous 1

    Quaternion quatBase;
    Quaternion quatCurrent;
    Quaternion quatPrevious;

    float currTime = 0.0f;
    float prevTime = 0.0f;


    void Start()
    {
        string[] csvFiles = Directory.GetFiles( rawDataFolderPath , "*.csv");

        foreach (string filePath in csvFiles)
        {
            ResetData ( );
            ProcessCsv ( filePath);
        }

        Debug.Log("모든 CSV 파일 처리 완료!");
    }

    void ResetData ( )
    {
        acceleration1 = Vector3. zero;
        acceleration2 = Vector3. zero;

        velocity1 = Vector3. zero;   // Previous 1
        velocity2 = Vector3. zero;   // Previous 1

        quatBase = Quaternion. identity;
        quatCurrent = Quaternion. identity;
        quatPrevious = Quaternion. identity;

        currTime = 0.0f;
        prevTime = 0.0f;

    }

    void ProcessCsv ( string filePath )
    {
        string [ ] lines = File. ReadAllLines ( filePath );
        if ( lines. Length < 2 )
        {
            Debug. LogWarning ( filePath + " 파일에 데이터 없음." );
            return;
        }

        string [ ] headerColumns = lines [ 0 ]. Split ( ',' );

        int quatN1Index = System. Array. IndexOf ( headerColumns , "Quat_1" );
        int quatN2Index = System. Array. IndexOf ( headerColumns , "Quat_2" );
        int quatN3Index = System. Array. IndexOf ( headerColumns , "Quat_3" );
        int quatN4Index = System. Array. IndexOf ( headerColumns , "Quat_4" );

        if ( quatN1Index == -1 || quatN2Index == -1 || quatN3Index == -1 || quatN4Index == -1 )
        {
            Debug. LogError ( "쿼터니언 데이터 칼럼이 없습니다." );
            return;
        }

        string header = lines [ 0 ] + ",Acc_NX,Acc_NY,Acc_NZ,Vel_NX,Vel_NY,Vel_NZ,Pos_X,Pos_Y,Pos_Z,Dist_X,Dist_Y,Dist_Z,Quat_X,Quat_Y,Quat_Z,Quat_W";
        List<string> outputLines = new List<string> { header };

        Vector3 position = Vector3. zero;
        Vector3 cumulativeDistance = Vector3. zero;
        float deltaTime = 0.0f;

        // 1. First Line Parsing
        string [ ] values = lines [ 1 ]. Split ( ',' );
        currTime = float. Parse ( values [ 0 ] );

        acceleration1 = new Vector3 ( float. Parse ( values [ 1 ] ) + 1.0f , float. Parse ( values [ 2 ] ) , float. Parse ( values [ 3 ] ) );
        quatBase = new Quaternion (
            float. Parse ( values [ quatN2Index ] ) ,
            float. Parse ( values [ quatN3Index ] ) ,
            float. Parse ( values [ quatN4Index ] ),
            float. Parse ( values [ quatN1Index ] )

        );
        quatBase = Quaternion. Inverse ( quatBase );

        ChangeDataAfterCalculate ( );

        // 2. Second Line Parsing
        values = lines [ 2 ]. Split ( ',' );
        currTime = float. Parse ( values [ 0 ] );
        deltaTime = currTime - prevTime;

        acceleration1 = new Vector3 ( float. Parse ( values [ 1 ] ) + 1.0f , float. Parse ( values [ 2 ] ) , float. Parse ( values [ 3 ] ) );

        velocity1 += ( acceleration1 * deltaTime );

        ChangeDataAfterCalculate ( );

        for ( int i = 3 ; i < lines. Length ; i++ )
        {
            values = lines [ i ]. Split ( ',' );

            // Calculate Delta Time
            currTime = float. Parse ( values [ 0 ] );
            deltaTime = currTime - prevTime;

            acceleration1 = new Vector3 ( float. Parse ( values [ 1 ] ) + 1.0f , float. Parse ( values [ 2 ] ) , float. Parse ( values [ 3 ] ) );
            Quaternion quat1 = new Quaternion (
                float. Parse ( values [ quatN1Index ] ) ,
                float. Parse ( values [ quatN2Index ] ) ,
                float. Parse ( values [ quatN3Index ] ) ,
                float. Parse ( values [ quatN4Index ] )
            );
            quatCurrent = quat1 * quatBase;

            if ( Quaternion. Dot ( quatPrevious , quatCurrent ) < 0 )
            {
                Debug. Log ( "튀는위치 =" + i );
                Debug. Log ( quatCurrent );

                quatCurrent. Set ( -quatCurrent. x , -quatCurrent. y , -quatCurrent. z , -quatCurrent. w );
                Debug. Log ( quatCurrent );

            }

            velocity1 += ( acceleration2 * deltaTime );
            Vector3 delta_s = velocity2 * deltaTime;
            position += delta_s;

            Vector3 delta_s_abs = new Vector3 ( Mathf. Abs ( delta_s. x ) , Mathf. Abs ( delta_s. y ) , Mathf. Abs ( delta_s. z ) );
            cumulativeDistance += delta_s_abs;

            string newLine = lines [ i ] + $",{acceleration1. x},{acceleration1. y},{acceleration1. z},{velocity1. x},{velocity1. y},{velocity1. z},{position. x},{position. y},{position. z},{cumulativeDistance. x},{cumulativeDistance. y},{cumulativeDistance. z}, {quatCurrent. x}, {quatCurrent. y}, {quatCurrent. z}, {quatCurrent. w}";
            outputLines. Add ( newLine );

            ChangeDataAfterCalculate ( );
        }

        if ( !Directory. Exists ( convertDataFolderPath ) )
        {
            Directory. CreateDirectory ( convertDataFolderPath );
        }

        string fileNameWithoutExt = Path. GetFileNameWithoutExtension ( filePath );
        string newFilePath = Path. Combine ( convertDataFolderPath , fileNameWithoutExt + "_convert.csv" );

        File. WriteAllLines ( newFilePath , outputLines. ToArray ( ) );

        Debug. Log ( "처리 완료: " + Path. GetFileName ( newFilePath ) );
    }

    void ChangeDataAfterCalculate()
    {     
        acceleration2 = acceleration1;
        velocity2 = velocity1;

        prevTime = currTime;
        quatPrevious = quatCurrent;
    }
}

