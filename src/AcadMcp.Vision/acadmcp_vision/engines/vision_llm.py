"""Vision LLM adapter (Anthropic Claude / OpenAI GPT-4o).

Per rule 32, trap #6: cap image at 1568 px long side, JPEG q85, refuse > 5 MB.
"""

from __future__ import annotations

import base64
import importlib
import io
import os
from dataclasses import dataclass

from PIL import Image

MAX_LONG_SIDE = 1568
MAX_PAYLOAD_BYTES = 5 * 1024 * 1024


@dataclass
class LlmReply:
    provider: str
    model: str
    text: str


def _shrink_to_jpeg(image: Image.Image) -> bytes:
    img = image
    long_side = max(img.size)
    if long_side > MAX_LONG_SIDE:
        scale = MAX_LONG_SIDE / long_side
        img = img.resize((int(img.size[0] * scale), int(img.size[1] * scale)), Image.LANCZOS)
    buf = io.BytesIO()
    img.save(buf, format="JPEG", quality=85, optimize=True)
    data = buf.getvalue()
    if len(data) > MAX_PAYLOAD_BYTES:
        raise ValueError(
            f"Image too large after JPEG q85: {len(data)} bytes "
            f"(cap {MAX_PAYLOAD_BYTES})"
        )
    return data


def has_anthropic_key() -> bool:
    return bool(os.environ.get("ANTHROPIC_API_KEY"))


def has_openai_key() -> bool:
    return bool(os.environ.get("OPENAI_API_KEY"))


def has_google_key() -> bool:
    # Accept either canonical spelling (SDK honours both).
    return bool(os.environ.get("GOOGLE_API_KEY") or os.environ.get("GEMINI_API_KEY"))


def describe(image: Image.Image, prompt: str, provider: str, max_tokens: int) -> LlmReply:
    if provider == "auto":
        if has_anthropic_key():
            provider = "anthropic"
        elif has_openai_key():
            provider = "openai"
        elif has_google_key():
            provider = "google"
        else:
            raise RuntimeError(
                "No vision LLM provider available. "
                "Set ANTHROPIC_API_KEY, OPENAI_API_KEY or GOOGLE_API_KEY."
            )
    if provider == "anthropic":
        return _anthropic(image, prompt, max_tokens)
    if provider == "openai":
        return _openai(image, prompt, max_tokens)
    if provider in ("google", "gemini"):
        return _google(image, prompt, max_tokens)
    raise ValueError(f"Unknown provider: {provider!r}")


def _anthropic(image: Image.Image, prompt: str, max_tokens: int) -> LlmReply:
    if not has_anthropic_key():
        raise RuntimeError("ANTHROPIC_API_KEY is not set.")
    try:
        anthropic = importlib.import_module("anthropic")
    except ImportError as ex:
        raise ImportError("Run `pip install anthropic`.") from ex
    client = anthropic.Anthropic()
    jpeg = _shrink_to_jpeg(image)
    b64 = base64.b64encode(jpeg).decode("ascii")
    model = os.environ.get("ACADMCP_ANTHROPIC_MODEL", "claude-3-5-sonnet-latest")
    msg = client.messages.create(
        model=model,
        max_tokens=max_tokens,
        messages=[
            {
                "role": "user",
                "content": [
                    {
                        "type": "image",
                        "source": {
                            "type": "base64",
                            "media_type": "image/jpeg",
                            "data": b64,
                        },
                    },
                    {"type": "text", "text": prompt},
                ],
            }
        ],
    )
    text = "".join(getattr(c, "text", "") for c in msg.content if getattr(c, "type", "") == "text")
    return LlmReply(provider="anthropic", model=model, text=text.strip())


def _openai(image: Image.Image, prompt: str, max_tokens: int) -> LlmReply:
    if not has_openai_key():
        raise RuntimeError("OPENAI_API_KEY is not set.")
    try:
        openai = importlib.import_module("openai")
    except ImportError as ex:
        raise ImportError("Run `pip install openai`.") from ex
    client = openai.OpenAI()
    jpeg = _shrink_to_jpeg(image)
    b64 = base64.b64encode(jpeg).decode("ascii")
    model = os.environ.get("ACADMCP_OPENAI_MODEL", "gpt-4o")
    rsp = client.chat.completions.create(
        model=model,
        max_tokens=max_tokens,
        messages=[
            {
                "role": "user",
                "content": [
                    {"type": "text", "text": prompt},
                    {
                        "type": "image_url",
                        "image_url": {"url": f"data:image/jpeg;base64,{b64}"},
                    },
                ],
            }
        ],
    )
    text = (rsp.choices[0].message.content or "").strip()
    return LlmReply(provider="openai", model=model, text=text)


def _google(image: Image.Image, prompt: str, max_tokens: int) -> LlmReply:
    """Google Gemini vision adapter.

    Default model: `gemini-3.1-pro-preview` - the April 2026 frontier vision
    model (released 2026-02-19, leads MMMU-Pro / Video-MME / DocVQA). We pin
    to the preview track explicitly because `gemini-3-pro-preview` was shut
    down on 2026-03-09.

    Model selection / reasoning controls:
    - `ACADMCP_GOOGLE_MODEL`         - override the model ID. Accepts any
                                       Gemini 3.1 ID, including
                                       `gemini-3.1-pro-preview-customtools`,
                                       `gemini-3.1-flash-lite-preview`, etc.
    - `ACADMCP_GOOGLE_THINKING`      - thinking level for Gemini 3 thinking
                                       models (one of `low`, `medium`,
                                       `high`, `max`). Defaults to `low`
                                       because Gemini 3.x counts reasoning
                                       tokens against `max_output_tokens`,
                                       and the architect-review persona
                                       asks for a structured 17-row JSON
                                       scorecard - at `high`/`max` the
                                       model spends nearly the whole
                                       budget on thinking and truncates
                                       the JSON mid-row. Callers that
                                       want slower-but-deeper reasoning
                                       should opt in explicitly AND raise
                                       `max_tokens` to >= 8000.

    Prefers the modern `google-genai` SDK; falls back to the legacy
    `google-generativeai` package so dev boxes without the newer SDK still
    work. The legacy SDK cannot pass a `thinking_config`, so thinking_level
    is silently ignored in the fallback path.
    """
    if not has_google_key():
        raise RuntimeError("GOOGLE_API_KEY / GEMINI_API_KEY is not set.")
    jpeg = _shrink_to_jpeg(image)
    model = os.environ.get("ACADMCP_GOOGLE_MODEL", "gemini-3.1-pro-preview")
    thinking_level = os.environ.get("ACADMCP_GOOGLE_THINKING", "low").lower()
    api_key = os.environ.get("GOOGLE_API_KEY") or os.environ.get("GEMINI_API_KEY")

    # Try the new SDK first (google-genai, unified API).
    try:
        genai_new = importlib.import_module("google.genai")
    except ImportError:
        genai_new = None

    if genai_new is not None:
        types_mod = importlib.import_module("google.genai.types")
        client = genai_new.Client(api_key=api_key)
        image_part = types_mod.Part.from_bytes(data=jpeg, mime_type="image/jpeg")

        cfg_kwargs: dict = {"max_output_tokens": max_tokens}
        # Gemini 3.x thinking models accept `thinking_config`; older models
        # ignore it. We guard the `types.ThinkingConfig` attribute because
        # very old `google-genai` versions may not expose it yet.
        thinking_cls = getattr(types_mod, "ThinkingConfig", None)
        if thinking_cls is not None and thinking_level in ("low", "medium", "high", "max"):
            cfg_kwargs["thinking_config"] = thinking_cls(thinking_level=thinking_level)

        resp = client.models.generate_content(
            model=model,
            contents=[prompt, image_part],
            config=types_mod.GenerateContentConfig(**cfg_kwargs),
        )
        text = (getattr(resp, "text", None) or "").strip()
        return LlmReply(provider="google", model=model, text=text)

    # Fallback: legacy SDK (google-generativeai). Does NOT support thinking_level.
    try:
        legacy = importlib.import_module("google.generativeai")
    except ImportError as ex:
        raise ImportError(
            "Neither `google-genai` nor `google-generativeai` is installed. "
            "Run `pip install google-genai` (preferred) or "
            "`pip install google-generativeai`."
        ) from ex

    legacy.configure(api_key=api_key)
    img_part = {"mime_type": "image/jpeg", "data": jpeg}
    m = legacy.GenerativeModel(model)
    resp = m.generate_content(
        [prompt, img_part],
        generation_config={"max_output_tokens": max_tokens},
    )
    text = (getattr(resp, "text", "") or "").strip()
    return LlmReply(provider="google", model=model, text=text)
