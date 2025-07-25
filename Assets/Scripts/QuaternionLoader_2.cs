using System. Collections;
using System. Collections. Generic;
using UnityEngine;
using System. IO;

public class QuaternionLoader_2 : MonoBehaviour
{
    public string csvFilePath = "C:/Data/quaternion_data.csv"; // 파일 경로
    public GameObject targetObject;
    public float interval = 0.01111111f; // 프레임 간격 (초)
    private List<Quaternion> quaternions = new List<Quaternion> ( );

    void Start ( )
    {
        LoadQuaternions ( );
        StartCoroutine ( ApplyQuaternionsCoroutine ( ) );
    }

    void LoadQuaternions ( )
    {
        if ( !File. Exists ( csvFilePath ) )
        {
            Debug. LogError ( "CSV 파일을 찾을 수 없습니다: " + csvFilePath );
            return;
        }

        string [ ] data = File. ReadAllLines ( csvFilePath );

        // 헤더 제외하고 1행부터 끝까지 읽기
        for ( int i = 1 ; i < data. Length ; i++ )
        {
            string line = data [ i ];
            if ( string. IsNullOrWhiteSpace ( line ) )
                continue;

            string [ ] row = line. Split ( ',' );
            if ( row. Length < 11 )
            {
                Debug. LogWarning ( $"{i}행에 데이터가 부족합니다." );
                continue;
            }

            float x = float. Parse ( row [ 8 ] );
            float y = float. Parse ( row [ 9 ] );
            float z = float. Parse ( row [ 10 ] );
            float w = float. Parse ( row [ 7 ] );

            quaternions. Add ( new Quaternion ( x , y , z , w ) );
        }
    }

    IEnumerator ApplyQuaternionsCoroutine ( )
    {
        foreach ( Quaternion q in quaternions )
        {
            targetObject. transform. rotation = q;
            yield return new WaitForSeconds ( interval ); // 간격만큼 대기
        }
    }
}
