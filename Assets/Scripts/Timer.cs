using UnityEngine;
using TMPro;

public class Timer : MonoBehaviour
{
    public TextMeshProUGUI timerText; // UI에 시간 표시
    private float timer = 0f;
    private bool isCounting = false;
    private int state = 0; // 0: 대기, 1: 진행, 2: 멈춤

    void Update ( )
    {
        // 상태가 1(진행 중)일 때만 시간 카운트
        if ( state == 1 )
        {
            timer += Time. deltaTime;
            timerText. text = Mathf. FloorToInt ( timer ). ToString ( );
        }

        // 스페이스바 입력 감지
        if ( Input. GetKeyDown ( KeyCode. Space ) )
        {
            state++;

            if ( state > 2 ) // 상태가 3이면 초기화
                state = 1; // 다시 시작

            switch ( state )
            {
                case 1:
                    Debug. Log ( "타이머 시작" );
                    break;
                case 2:
                    Debug. Log ( "타이머 멈춤" );
                    break;
                case 3: // 안 쓰이지만 안정적으로 초기화 처리됨
                    break;
            }

            if ( state == 1 )
            {
                timer = 0f; // 초기화 후 시작
            }
        }
    }
}

