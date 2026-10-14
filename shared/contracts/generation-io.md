# 생성 입출력 계약 (Unity ↔ ComfyUI) v0.1

Unity 와 ComfyUI 워크플로가 서로 기대하는 값. 한쪽을 바꾸면 이 문서와 `comfyui/node-mapping.json` 을 같이 고친다.

## 입력 (Unity → ComfyUI)

| 항목 | 값 |
|---|---|
| 형식 | PNG, RGBA32 (알파 255), sRGB |
| 크기 | 1280 × 720 (16:9) — `PaintCanvas.textureWidth/Height` |
| 내용 | 드로잉 레이어 + 종이색 배경 `#FAF4E6` (250,244,230). UI·두루마리·액자 미포함 |
| 방향 | 위쪽이 위 (Texture2D 원점 보정 불필요, EncodeToPNG 결과 그대로) |
| 업로드 | `POST /upload/image` multipart `image`, `overwrite=true`, 파일명 `unity_paint_yyyyMMdd_HHmmss.png` |

## 실행 요청

`POST /prompt` body: `{"prompt": <치환된 워크플로>, "client_id": "<guid>"[, "extra_data": {"api_key_comfy_org": "..."}]}`

자리표시자: `{{INPUT_IMAGE}} {{POSITIVE}} {{NEGATIVE}} {{SEED}} {{STEPS}} {{CFG}} {{DENOISE}} {{CONTROL_STRENGTH}}` (노드는 node-mapping.json)

## 출력 (ComfyUI → Unity)

| 항목 | 값 |
|---|---|
| 노드 | SaveImage `12` (node-mapping.json `output.node`) |
| 크기 | 1344 × 768 (16:9, 입력과 같은 비율) |
| 조회 | `GET /history/{prompt_id}` → `outputs["12"].images[0]` → `GET /view?filename&subfolder&type=output` |

## 결과 메타데이터 (Unity 저장 · XR 전달)

`<persistentDataPath>/Results/<stamp>_meta.json` — 스키마: `result-meta.schema.json`
