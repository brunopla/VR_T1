using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Timer : MonoBehaviour
{
    public Text timer;
    public float time = 10f;
    public static bool win = false;


    // Start is called before the first frame update
    void Start()
    {
        timer = GetComponent<Text>();

        win = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (time > 0 && !win)
        {
            timer.text = time.ToString();
            time -= 1 * Time.deltaTime;
                    

        }
     
        //Debug.Log(time);
     
        else if(time <= 0)
        {   
            SceneManager.LoadScene(0);
        }

        

    }

    
  

}
