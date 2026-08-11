using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DevionGames.UIWidgets
{
    public class FloatingText : MonoBehaviour
    {
        private Transform m_Target;
        private Vector3 m_Offset;
        private Text m_Text;
        private Canvas m_Canvas;

        private void Awake()
        {
            this.m_Text = GetComponent<Text>();
            this.m_Canvas = GetComponentInParent<Canvas>();
        }


        private void LateUpdate()
        {
            if (this.m_Target == null || Camera.main == null) return;

            Vector3 pos = UnityTools.GetBounds(this.m_Target.gameObject).center + this.m_Offset;

            // World Space 캔버스(MR)에서는 스크린 좌표 변환이 의미가 없다 — 월드 위치를 그대로 쓴다.
            // WorldToScreenPoint(pos)를 World Space RectTransform.position에 대입하면 픽셀값이
            // 그대로 미터 좌표로 해석되어 오브젝트가 화면 밖 임의의 지점으로 날아간다.
            if (this.m_Canvas != null && this.m_Canvas.renderMode == RenderMode.WorldSpace)
            {
                Vector3 toTarget = pos - Camera.main.transform.position;
                this.m_Text.enabled = Vector3.Dot(toTarget, Camera.main.transform.forward) > 0f;
                transform.position = pos;
                return;
            }

            Vector3 screenPos = Camera.main.WorldToScreenPoint(pos);
            this.m_Text.enabled = screenPos.x > 0 && screenPos.x < Camera.main.pixelWidth && screenPos.y > 0 && screenPos.y < Camera.main.pixelHeight && screenPos.z > 0;
            transform.position = screenPos;
        }

        public void SetText(Transform target, string text, Color color, Vector3 offset) {
            this.m_Target = target;
            this.m_Offset = offset;
            Text component = GetComponent<Text>();
            component.text = text;
            component.color = color;

        }

      
    }
}