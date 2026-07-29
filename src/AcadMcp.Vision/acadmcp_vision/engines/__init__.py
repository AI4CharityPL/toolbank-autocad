"""Per-engine adapters for OCR / YOLO / SAM / vision-LLM. Each adapter is
self-contained and lazy-imports its heavy dep at first use (rule 32, trap #9).
"""
