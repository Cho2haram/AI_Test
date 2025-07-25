using System. Collections;
using System. Collections. Generic;
using UnityEngine;
using System. IO;

public class QuaternionLoader : MonoBehaviour
{
    public string csvFilePath = "C:/Data/quaternion_data.csv"; // 불러올 파일의 전체 경로
    public GameObject targetObject;
    public int targetLineIndex = 1; // 1부터 시작 (헤더 제외)

    void Start ( )
    {
        LoadAndApplyQuaternion ( );
    }

    void LoadAndApplyQuaternion ( )
    {
        if ( !File. Exists ( csvFilePath ) )
        {
            Debug. LogError ( "CSV 파일을 찾을 수 없습니다: " + csvFilePath );
            return;
        }

        string [ ] data = File. ReadAllLines ( csvFilePath );

        if ( targetLineIndex >= data. Length )
        {
            Debug. LogError ( "지정한 행 번호가 CSV 파일의 데이터 행 수보다 큽니다." );
            return;
        }

        string line = data [ targetLineIndex ];
        if ( string. IsNullOrWhiteSpace ( line ) )
        {
            Debug. LogError ( "해당 행에 데이터가 없습니다." );
            return;
        }

        string [ ] row = line. Split ( ',' );
        if ( row. Length < 4 )
        {
            Debug. LogError ( "해당 행에 쿼터니언 데이터가 부족합니다." );
            return;
        }

        float x = float. Parse ( row [ 8 ] );
        float y = float. Parse ( row [ 9 ] );
        float z = float. Parse ( row [ 10 ] );
        float w = float. Parse ( row [ 7 ] );

        Quaternion q = new Quaternion ( x , y , z , w );

        targetObject. transform. rotation = q;
        Debug. Log ( $"[{csvFilePath}]의 {targetLineIndex}번째 행 쿼터니언 적용: {q}" );
    }
}