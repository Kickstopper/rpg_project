using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

public class MatrixStream : MonoBehaviour
{
    [Header("Color Settings")]
    public Color streamColor = Color.green; // 꼬리 부분 색상
    public Color headColor = Color.white;   // 머리 부분 색상

    private TMP_Text _textComponent;
    private RectTransform _rectTransform; 
    private float _fallSpeed;
    private List<char> _chars = new List<char>();
    private StringBuilder _sb;
    private int _targetLength;
    private float _canvasHeight;

    private const string Glyphs = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!@#$%^&*()_+-=[]{}|;':,./<>?";

    public void Setup(float speed)
    {
        _textComponent = GetComponent<TMP_Text>();
        _rectTransform = GetComponent<RectTransform>();
        _fallSpeed = speed;
        _sb = new StringBuilder();

        RectTransform canvasRect = transform.parent.GetComponent<RectTransform>();
        _canvasHeight = canvasRect.rect.height;

        _targetLength = Random.Range(30, 60);

        StartCoroutine(GenerateStream());
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

        _fallSpeed = Random.Range(50f, 150f); 

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

        // 인스펙터에서 설정한 색상을 HTML 헥스 코드로 변환
        string headColorHex = ColorUtility.ToHtmlStringRGB(headColor);
        string streamColorHex = ColorUtility.ToHtmlStringRGB(streamColor);

        for (int i = 0; i < count; i++)
        {
            bool isHead = (i == count - 1);

            if (isHead)
            {
                // 머리 부분 색상 적용
                _sb.Append($"<color=#{headColorHex}><b>{_chars[i]}</b></color>");
            }
            else
            {
                float alpha = Mathf.Lerp(0.1f, 1.0f, (float)i / (count - 1));
                string alphaHex = Mathf.FloorToInt(alpha * 255).ToString("X2");

                // 꼬리 부분 색상에 투명도 결합
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