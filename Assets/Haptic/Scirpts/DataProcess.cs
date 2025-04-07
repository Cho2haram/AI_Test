using System. Collections;
using System. Collections. Generic;
using System. IO; // ���� ������� ���� ���ӽ����̽�
using UnityEngine;
using HapticDllCs;

public class DataProcess : MonoBehaviour
{
   
    [Header("IMU")]
    HapticDll hapticDll;
    public List<float> AccelList; // ���ӵ� ������ (x, y, z)
    public List<float> GyroList; // ���̷ν����� ������ (x, y, z ���ӵ�)


    private void Start()
    {
        hapticDll = new HapticDll();
        hapticDll. InitializeHapticDevice(1 , Debug. Log);     
    }

    void Update()
    {
        IMU_DataReceive();
    }

    void IMU_DataReceive()
    {
        // IMU ������ ��������
        AccelList = hapticDll. GetAccelerometer();
        GyroList = hapticDll. GetGyroscope();
    }

}
