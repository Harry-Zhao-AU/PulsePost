"""Selects the best topic and generates a full article, X thread, and image prompt."""

import json
import os
import sys
from openai import AzureOpenAI

client = AzureOpenAI(
    azure_endpoint=os.environ["AZURE_OPENAI_ENDPOINT"],
    api_key=os.environ["AZURE_OPENAI_KEY"],
    api_version="2024-08-01-preview",
)
DEPLOYMENT = os.environ["AZURE_OPENAI_DEPLOYMENT"]

STYLE_REFERENCE = """
Harry writes in first-person, direct, opinionated tone. Technical but accessible.
He uses real-world examples, often from banking or logistics systems.
He avoids fluff — every paragraph makes a point.
Short sentences. Occasional rhetorical questions. No corporate speak.
"""


def select_topic(topics: dict) -> dict:
    prompt = f"""You are a content strategist for Harry Zhao, a Senior Software Engineer in Melbourne
specialising in AI-augmented systems, distributed platforms, and cloud-native services.

Here are this week's trending AI topics:

Hacker News:
{json.dumps(topics['hackernews'], indent=2)}

Official Company Blogs (Anthropic, OpenAI, Google DeepMind, Meta AI, Microsoft AI):
{json.dumps(topics['company_blogs'], indent=2)}

Reddit (r/MachineLearning, r/LocalLLaMA):
{json.dumps(topics['reddit'], indent=2)}

YouTube (Andrej Karpathy, Yannic Kilcher, Two Minute Papers, AI Explained, Matt Wolfe, DeepMind, Lex Fridman):
{json.dumps(topics['youtube'], indent=2)}

arXiv:
{json.dumps(topics['arxiv'], indent=2)}

Topics Harry has already written about:
{json.dumps(topics['existing_posts'], indent=2)}

Select the single most interesting and timely topic for a senior engineering audience.
Do NOT repeat existing topics.

Return JSON only:
{{
  "title": "article title",
  "angles": ["angle 1", "angle 2", "angle 3"],
  "why_now": "one sentence on why this is timely",
  "sources": [
    {{"name": "source title", "url": "https://...", "from": "hackernews|github|arxiv"}}
  ]
}}"""

    response = client.chat.completions.create(
        model=DEPLOYMENT,
        messages=[{"role": "user", "content": prompt}],
        response_format={"type": "json_object"},
        temperature=0.7,
    )
    return json.loads(response.choices[0].message.content)


def generate_article(topic: dict) -> str:
    prompt = f"""Write a technical blog article for Harry Zhao's blog (harry-zhao-au.github.io).

Topic: {topic['title']}
Key angles to cover:
{chr(10).join(f'- {a}' for a in topic['angles'])}

Why this matters now: {topic['why_now']}

Writing style:
{STYLE_REFERENCE}

Format: Markdown, 800-1200 words.
Include:
- A compelling opening that hooks the reader
- Code examples where relevant (C#, Python, or TypeScript)
- A practical takeaway at the end
- No generic conclusions like "In summary..."

Start directly with the article content. No preamble."""

    response = client.chat.completions.create(
        model=DEPLOYMENT,
        messages=[{"role": "user", "content": prompt}],
        temperature=0.7,
        max_tokens=2000,
    )
    return response.choices[0].message.content


def generate_x_thread(article: str, topic: dict) -> list[str]:
    prompt = f"""Convert this article into an X (Twitter) thread for Harry Zhao.

Article:
{article}

Rules:
- Tweet 1: compelling hook, max 280 chars, no hashtags
- Tweets 2-7: one key insight each, max 280 chars
- Final tweet: "Full article on my blog 👇" (blog link added separately)
- Write in Harry's voice: direct, technical, no fluff
- No emojis except sparingly

Return JSON only:
{{"tweets": ["tweet 1", "tweet 2", ...]}}"""

    response = client.chat.completions.create(
        model=DEPLOYMENT,
        messages=[{"role": "user", "content": prompt}],
        response_format={"type": "json_object"},
        temperature=0.7,
    )
    return json.loads(response.choices[0].message.content)["tweets"]


def generate_image_prompt(topic: dict, article: str) -> str:
    prompt = f"""You are a technical illustrator generating a DALL-E cover image for a software engineering blog.

Article title: {topic['title']}
Article content (first 600 chars): {article[:600]}

Your job: Extract one of the following from the article — whichever best represents its core idea — and visualise it as a clean technical illustration:

- **WORKFLOW** — a sequence of steps or data flow (e.g. request → process → response)
- **ARCHITECTURE** — system components and how they connect (e.g. layers, services, APIs)
- **PILLARS** — the key principles or concepts the article is built around (e.g. 3-4 named pillars displayed as columns or blocks)

Steps:
1. Decide which of the three fits best for this article
2. Identify the specific named components, steps, or pillars from the article content
3. Describe the layout and flow clearly so DALL-E can render it accurately

Good examples:
- Workflow: "A left-to-right flowchart: User Request → Orchestrator → Tool Caller → External API → Response. Dark navy background, white boxes, teal arrows, flat technical style."
- Architecture: "Three vertical layers stacked: top layer labeled 'API Gateway', middle labeled 'Event Bus with Kafka', bottom labeled 'Microservices'. Connecting arrows between layers. Dark background, monochrome with amber highlights."
- Pillars: "Four labeled rectangular modules arranged side by side: Observability, Resilience, Scalability, Security. Each module is a clean card with an icon above the label. Dark slate background, white text, flat design."

BANNED (do not use):
- People, developers, human figures
- Glowing nodes, generic circuits, neural network blobs
- Puzzle pieces, gears, lightbulbs
- Vague words like 'futuristic', 'innovative', 'seamless'

The diagram must reflect the SPECIFIC components and flow from THIS article, not a generic AI system.
Always include the article title "{topic['title']}" as a visible heading at the top of the image in clean bold white text.

Return only the final DALL-E prompt, 2-3 sentences."""

    response = client.chat.completions.create(
        model=DEPLOYMENT,
        messages=[{"role": "user", "content": prompt}],
        temperature=0.8,
    )
    return response.choices[0].message.content.strip()


if __name__ == "__main__":
    topics = json.loads(sys.stdin.read())

    print("Selecting topic...", file=sys.stderr)
    topic = select_topic(topics)
    print(f"Selected: {topic['title']}", file=sys.stderr)

    print("Generating article...", file=sys.stderr)
    article = generate_article(topic)

    print("Generating X thread...", file=sys.stderr)
    x_thread = generate_x_thread(article, topic)

    print("Generating image prompt...", file=sys.stderr)
    image_prompt = generate_image_prompt(topic, article)

    draft = {
        "topic": topic,
        "article": article,
        "x_thread": x_thread,
        "image_prompt": image_prompt,
    }

    print(json.dumps(draft, indent=2))
