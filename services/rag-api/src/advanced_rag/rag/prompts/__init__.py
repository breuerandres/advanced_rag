"""Prompt files for the RAG pipeline.

System prompts and condensation prompts live as Markdown files keyed by locale:
    system_<locale>.md
    condenser_<locale>.md
    rewriter_<locale>.md

Loaders should default to the tenant's configured locale and fall back to en-US.
"""
