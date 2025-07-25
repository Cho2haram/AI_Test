using System;
using System. Collections. Generic;
using System. IO;
using UnityEngine;

public class AccelToPositionWithTimestamp : MonoBehaviour
{
    public string folderPath = "C:/YourFolder";

    void Start ( )
    {
        string [ ] csvFiles = Directory. GetFiles ( folderPath , "*.csv" );

        foreach ( string filePath in csvFiles )
        {
            ProcessCsv ( filePath );
        }

        Debug. Log ( "모든 CSV 파일 처리 완료!" );
    }

    void ProcessCsv ( string filePath )
    {
        string [ ] lines = File. ReadAllLines ( filePath );
        if ( lines. Length < 2 )
        {
            Debug. LogWarning ( filePath + " 파일에 데이터 없음." );
            return;
        }

        string header = lines [ 0 ] + ",Pos_X,Pos_Y,Pos_Z,Dist_X,Dist_y,Dist_z";
        List<string> outputLines = new List<string> { header };

        Vector3 velocity = Vector3. zero;
        Vector3 position = Vector3. zero;
        float cumulativeDx = 0f;
        float cumulativeDy = 0f;
        float cumulativeDz = 0f;

        float prevTime = float. Parse ( lines [ 1 ]. Split ( ',' ) [ 0 ] );

        for ( int i = 1 ; i < lines. Length ; i++ )
        {
            string [ ] values = lines [ i ]. Split ( ',' );

            float currTime = float. Parse ( values [ 0 ] );
            float deltaTime = currTime - prevTime;

            float ax = float. Parse ( values [ 1 ] );
            float ay = float. Parse ( values [ 2 ] );
            float az = float. Parse ( values [ 3 ] );

            Vector3 accel = new Vector3 ( ax , ay , az );

            // 속도, 위치 적분
            velocity += accel * deltaTime;
            position += velocity * deltaTime;

            // 속도의 절댓값 × dt로 거리 계산
            float dx = Mathf. Abs ( velocity. x ) * deltaTime;
            float dy = Mathf. Abs ( velocity. y ) * deltaTime;
            float dz = Mathf. Abs ( velocity. z ) * deltaTime;

            // 누적 거리 더하기
            cumulativeDx += dx;
            cumulativeDy += dy;
            cumulativeDz += dz;

            // 새로운 라인 작성
            string newLine = lines [ i ] + $",{position. x},{position. y},{position. z},{cumulativeDx},{cumulativeDy},{cumulativeDz}";
            outputLines. Add ( newLine );

            // 이전 timestamp 갱신
            prevTime = currTime;
        }

        // 새 파일 경로 생성: 원본이름_position.csv
        string directory = Path. GetDirectoryName ( filePath );
        string fileNameWithoutExt = Path. GetFileNameWithoutExtension ( filePath );
        string newFilePath = Path. Combine ( directory , fileNameWithoutExt + "_position.csv" );

        // 새 파일로 저장
        File. WriteAllLines ( newFilePath , outputLines. ToArray ( ) );

        Debug. Log ( "처리 완료: " + Path. GetFileName ( newFilePath ) );
    }
}

