using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

public class MatrixStream : MonoBehaviour
{
    [Header("Color Settings")]
    public Color streamColor = Color.green;
    public Color headColor = Color.white;  

    private TMP_Text _textComponent;
    private RectTransform _rectTransform; 
    private float _fallSpeed;
    private List<char> _chars = new List<char>();
    private StringBuilder _sb;
    private int _targetLength;
    private float _canvasHeight;

    // 속도 유지와 코루틴 제어를 위한 변수
    private float _minSpeed;
    private float _maxSpeed;
    private Coroutine _generateRoutine;

    private const string Glyphs = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!@#$%^&*()_+-=[]{}|;':,./<>?";

    // 고정된 속도가 아닌 최소/최대 속도를 받아와서 기억
    public void Setup(float minSpd, float maxSpd)
    {
        _textComponent = GetComponent<TMP_Text>();
        _rectTransform = GetComponent<RectTransform>();
        _sb = new StringBuilder();

        _minSpeed = minSpd;
        _maxSpeed = maxSpd;
        _fallSpeed = Random.Range(_minSpeed, _maxSpeed);

        RectTransform canvasRect = transform.parent.GetComponent<RectTransform>();
        _canvasHeight = canvasRect.rect.height;

        _targetLength = Random.Range(30, 60);

        if (_generateRoutine != null) StopCoroutine(_generateRoutine);
        _generateRoutine = StartCoroutine(GenerateStream());
    }

    // UI가 다시 켜질 때 죽어버린 코루틴을 다시 살림
    private void OnEnable()
    {
        // _sb가 null이 아니라는 것은 Setup이 이미 완료된 재활용 객체라는 뜻
        if (_sb != null)
        {
            if (_generateRoutine != null) StopCoroutine(_generateRoutine);
            _generateRoutine = StartCoroutine(GenerateStream());
        }
    }

    // UI가 꺼질 때 코루틴을 깔끔하게 정리합니다.
    private void OnDisable()
    {
        if (_generateRoutine != null)
        {
            StopCoroutine(_generateRoutine);
            _generateRoutine = null;
        }
    }

    private void Update()
    {
        _rectTransform.anchoredPosition += Vector2.down * _fallSpeed * Time.deltaTime;

        if (_rectTransform.anchoredPosition.y < -_canvasHeight * 1.5f) 
        {
            ResetPosition();
        }
    }

    private void ResetPosition()
    {
        _chars.Clear();
        _textComponent.text = "";
        _targetLength = Random.Range(30, 60);

        // 처음에 받았던 세팅 값을 유지
        _fallSpeed = Random.Range(_minSpeed, _maxSpeed); 

        float topResetY = (_canvasHeight / 2f) + Random.Range(100f, 1500f);
        _rectTransform.anchoredPosition = new Vector2(_rectTransform.anchoredPosition.x, topResetY);
    }

    private IEnumerator GenerateStream()
    {
        while (true)
        {
            _chars.Add(GetRandomChar());

            if (_chars.Count > _targetLength)
            {
                _chars.RemoveAt(0);
            }

            if (_chars.Count > 0)
            {
                if (Random.value > 0.3f) _chars[_chars.Count - 1] = GetRandomChar();

                int randomIdx = Random.Range(0, _chars.Count);
                if (Random.value > 0.9f) _chars[randomIdx] = GetRandomChar();
            }

            _textComponent.text = GetStylizedString();

            yield return new WaitForSeconds(Random.Range(0.03f, 0.08f));
        }
    }

    private string GetStylizedString()
    {
        _sb.Clear();
        int count = _chars.Count;

        string headColorHex = ColorUtility.ToHtmlStringRGB(headColor);
        string streamColorHex = ColorUtility.ToHtmlStringRGB(streamColor);

        for (int i = 0; i < count; i++)
        {
            bool isHead = (i == count - 1);

            if (isHead)
            {
                _sb.Append($"<color=#{headColorHex}><b>{_chars[i]}</b></color>");
            }
            else
            {
                float alpha = Mathf.Lerp(0.1f, 1.0f, (float)i / (count - 1));
                string alphaHex = Mathf.FloorToInt(alpha * 255).ToString("X2");

                _sb.Append($"<color=#{streamColorHex}{alphaHex}>{_chars[i]}</color>\n");
            }
        }
        return _sb.ToString();
    }

    private char GetRandomChar()
    {
        return Glyphs[Random.Range(0, Glyphs.Length)];
    }
}