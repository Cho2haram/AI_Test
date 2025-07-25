using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine. UI;
using HapticDllCs;

public class ModelActivation : MonoBehaviour
{
    public Button OrangeButton, DrumButton, RainbowButton, SupermanButton, DumbbellButton;
    public GameObject OrangeClassifier, DrumClassifier, RainbowClassifier, SupermanClassifier, DumbbellClassifier;

    private bool isAActive = false;
    private bool isBActive = false;
    private bool isCActive = false;
    private bool isDActive = false;
    private bool isEActive = false;


    void Start ( )
    {
        OrangeButton. onClick. AddListener ( ( ) => ToggleObject ( "A" ) );
        DrumButton. onClick. AddListener ( ( ) => ToggleObject ( "B" ) );
        RainbowButton. onClick. AddListener ( ( ) => ToggleObject ( "C" ) );
        SupermanButton. onClick. AddListener ( ( ) => ToggleObject ( "D" ) );
        DumbbellButton. onClick. AddListener ( ( ) => ToggleObject ( "E" ) );


    }

    void ToggleObject ( string objectType )
    {
        switch ( objectType )
        {
            case "A":
                isAActive = !isAActive;
                OrangeClassifier. SetActive ( isAActive );
                SetButtonInteractable ( OrangeButton , isAActive );
                break;

            case "B":
                isBActive = !isBActive;
                DrumClassifier. SetActive ( isBActive );
                SetButtonInteractable ( DrumButton , isBActive );
                break;

            case "C":
                isCActive = !isCActive;
                RainbowClassifier. SetActive ( isCActive );
                SetButtonInteractable ( RainbowButton , isCActive );
                break;

            case "D":
                isDActive = !isDActive;
                SupermanClassifier. SetActive ( isDActive );
                SetButtonInteractable ( SupermanButton , isDActive );
                break;

            case "E":
                isEActive = !isEActive;
                DumbbellClassifier. SetActive ( isEActive );
                SetButtonInteractable ( DumbbellButton , isEActive );
                break;

        }
    }

    void SetButtonInteractable ( Button activeButton , bool isActive )
    {
        OrangeButton. interactable = !isActive || activeButton == OrangeButton;
        DrumButton. interactable = !isActive || activeButton == DrumButton;
        RainbowButton. interactable = !isActive || activeButton == RainbowButton;
        SupermanButton. interactable = !isActive || activeButton == SupermanButton;
        DumbbellButton. interactable = !isActive || activeButton == DumbbellButton;

    }


}


