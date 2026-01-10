using System.Collections;
using System.Collections.Generic; // List 사용을 위해 필요
using System.Text;
using UnityEngine;
using TMPro;

public class MatrixStream : MonoBehaviour
{
    private TMP_Text _textComponent;
    private float _fallSpeed;
    private List<char> _chars = new List<char>(); // 배열 대신 리스트 사용
    private StringBuilder _sb;
    private int _targetLength; // 이 스트림이 가질 최대 길이

    // 특수문자 포함 (반각 가타카나 느낌)
    private const string Glyphs = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!@#$%^&*()_+-=[]{}|;':,./<>?";

    public void Setup(float speed)
    {
        _textComponent = GetComponent<TMP_Text>();
        _fallSpeed = speed;
        _sb = new StringBuilder();

        // 스트림마다 최대 길이를 다르게 설정 (30 ~ 60)
        _targetLength = Random.Range(30, 60);

        // 코루틴 시작
        StartCoroutine(GenerateStream());
    }

    private void Update()
    {
        // 1. 물리적 낙하 (전체 위치 이동)
        transform.Translate(Vector3.down * _fallSpeed * Time.deltaTime);

        // 2. 화면 밖으로 완전히 나가면 리셋
        // 꼬리가 길어지므로 여유 있게 화면 높이의 1.5배 정도 내려가면 리셋
        if (transform.position.y < -Screen.height * 1.5f) 
        {
            ResetPosition();
        }
    }

    private void ResetPosition()
    {
        // 리스트를 비워서 다시 '한 글자부터' 시작하게 만듦
        _chars.Clear();
        _textComponent.text = "";

        // 새 길이 랜덤 설정
        _targetLength = Random.Range(30, 60);

        // 위치를 화면 위쪽 랜덤한 곳으로 이동
        Vector3 newPos = transform.position;
        // Y좌표: 화면 꼭대기 근처에서 시작하거나, 약간 위에서 시작
        newPos.y = Screen.height / 2 + Random.Range(100f, 500f);
        transform.position = newPos;
    }

    private IEnumerator GenerateStream()
    {
        while (true)
        {
            // 1. 새 글자 추가 (Head)
            _chars.Add(GetRandomChar());

            // 2. 최대 길이를 넘으면 맨 뒤(위쪽, Tail) 글자 삭제
            if (_chars.Count > _targetLength)
            {
                _chars.RemoveAt(0);
            }

            // 3. 기존 글자들 중 일부를 랜덤하게 변경 (글리치 효과)
            // 리스트가 비어있지 않다면
            if (_chars.Count > 0)
            {
                // 맨 아래(Head)는 더 자주 바뀌게
                if (Random.value > 0.3f) _chars[_chars.Count - 1] = GetRandomChar();

                // 중간 글자들도 가끔 변경
                int randomIdx = Random.Range(0, _chars.Count);
                if (Random.value > 0.9f) _chars[randomIdx] = GetRandomChar();
            }

            // 4. 문자열 조립 및 적용
            _textComponent.text = GetStylizedString();

            // 생성 속도 (이 값이 작을수록 글자가 빨리 자라남)
            // 낙하 속도와 별개로 글자가 타이핑되는 속도감 조절
            yield return new WaitForSeconds(Random.Range(0.03f, 0.08f));
        }
    }

    private string GetStylizedString()
    {
        _sb.Clear();
        int count = _chars.Count;

        for (int i = 0; i < count; i++)
        {
            // 리스트의 마지막 요소가 화면상 '맨 아래(Head)'입니다.
            bool isHead = (i == count - 1);

            if (isHead)
            {
                // 헤드: 흰색, 굵게, 불투명
                // *아직 길이가 짧을 때도 헤드는 무조건 흰색으로 나옵니다*
                _sb.Append($"<color=#FFFFFF><b>{_chars[i]}</b></color>");
            }
            else
            {
                // 꼬리: 위쪽(인덱스 0)일수록 투명해짐
                // 현재 길이(count)를 기준으로 알파값 계산
                float alpha = Mathf.Lerp(0.1f, 1.0f, (float)i / (count - 1));
                string alphaHex = Mathf.FloorToInt(alpha * 255).ToString("X2");

                _sb.Append($"<color=#00FF00{alphaHex}>{_chars[i]}</color>\n");
            }
        }
        return _sb.ToString();
    }

    private char GetRandomChar()
    {
        return Glyphs[Random.Range(0, Glyphs.Length)];
    }
}