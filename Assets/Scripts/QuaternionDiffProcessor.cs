using System;
using System. Collections. Generic;
using System. IO;
using UnityEngine;

public class QuaternionDiffProcessor : MonoBehaviour
{
    public string sourceFolderPath = "C:/YourFolder";  // 원본 폴더
    public string saveFolderPath = "C:/YourFolder/Processed";  // 저장 폴더

    // 기준 쿼터니언 값 (예: B 값)
    public Quaternion targetQuaternion = new Quaternion ( 0.99996f , -0.008973045f , 0.0004364792f , -0.0001839362f );

    float a1 = 0.99996f;
    float a2 = -0.008973045f;
    float a3 = 0.0004364792f;
    float a4 = -0.0001839362f;

    void Start ( )
    {
        // 폴더에 있는 모든 CSV 파일 경로 가져오기
        string [ ] csvFiles = Directory. GetFiles ( sourceFolderPath , "*.csv" );

        // 저장 폴더 없으면 생성
        if ( !Directory. Exists ( saveFolderPath ) )
            Directory. CreateDirectory ( saveFolderPath );

        foreach ( string filePath in csvFiles )
        {
            string [ ] lines = File. ReadAllLines ( filePath );
            List<string> newLines = new List<string> ( );

            // 헤더에 diff 값 추가
            string header = lines [ 0 ] + ",diff_x,diff_y,diff_z,diff_w";
            newLines. Add ( header );

            // 데이터 처리
            for ( int i = 1 ; i < lines. Length ; i++ )
            {
                string [ ] values = lines [ i ]. Split ( ',' );

                float qx = float. Parse ( values [ 7 ] );
                float qy = float. Parse ( values [ 8 ] );
                float qz = float. Parse ( values [ 9 ] );
                float qw = float. Parse ( values [ 10 ] );

                Quaternion A = new Quaternion ( qx , qy , qz , qw );
                Quaternion diff = Quaternion. Inverse ( targetQuaternion ) * A;

                string newLine = lines [ i ] + $",{diff. x},{diff. y},{diff. z},{diff. w}";
                newLines. Add ( newLine );
            }

            // 파일 이름만 추출
            string fileName = Path. GetFileName ( filePath );
            string savePath = Path. Combine ( saveFolderPath , fileName );

            // 새 CSV 저장
            File. WriteAllLines ( savePath , newLines. ToArray ( ) );
        }

        Debug. Log ( "CSV 변환 및 저장 완료!" );
    }
}
