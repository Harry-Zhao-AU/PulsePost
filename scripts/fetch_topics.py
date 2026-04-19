"""Fetches trending AI topics from HN, company blogs, Reddit, YouTube, and arXiv."""

import json
import os
import re
import sys
import time
import feedparser
import requests

HN_KEYWORDS = ["ai", "llm", "agent", "mcp", "rag", "vector", "gpt", "claude",
               "neural", "transformer", "embedding", "copilot", "openai", "anthropic",
               "gemini", "mistral", "llama", "deepmind", "diffusion"]

COMPANY_FEEDS = [
    {"name": "Anthropic",   "url": "https://www.anthropic.com/rss.xml"},
    {"name": "OpenAI",      "url": "https://openai.com/blog/rss.xml"},
    {"name": "Google DeepMind", "url": "https://deepmind.google/blog/rss.xml"},
    {"name": "Meta AI",     "url": "https://ai.meta.com/blog/rss/"},
    {"name": "Microsoft AI","url": "https://blogs.microsoft.com/ai/feed/"},
]

REDDIT_SUBREDDITS = ["MachineLearning", "LocalLLaMA"]

YOUTUBE_CHANNELS = [
    {"name": "Andrej Karpathy",   "channel_id": "UCnUYZLuoy1rq1aVMwx4aTzw"},
    {"name": "Yannic Kilcher",    "channel_id": "UCZHmQk67mSJgfCCTn7xBfew"},
    {"name": "Two Minute Papers", "channel_id": "UCbfYPyITQ-7l4upoX8nvctg"},
    {"name": "AI Explained",      "channel_id": "UCNJ1Ymd5yFuUPtn21xtRbbw"},
    {"name": "Matt Wolfe",        "channel_id": "UCb_X2sCuGPRMf4_A3K0H_cw"},
    {"name": "Google DeepMind",   "channel_id": "UCP7jMXSY2xbc3KCAE0MHQ-A"},
    {"name": "Lex Fridman",       "channel_id": "UCSHZKyawb77ixDdsGog4iWA"},
]

GH_HEADERS = {
    "Authorization": f"Bearer {os.environ['GH_PAT']}",
    "Accept": "application/vnd.github+json",
}


def fetch_hacker_news():
    top_ids = requests.get(
        "https://hacker-news.firebaseio.com/v0/topstories.json", timeout=10
    ).json()

    results = []
    for story_id in top_ids[:200]:
        story = requests.get(
            f"https://hacker-news.firebaseio.com/v0/item/{story_id}.json", timeout=10
        ).json()
        title = (story.get("title") or "").lower()
        if any(kw in title for kw in HN_KEYWORDS):
            results.append({
                "source": "hackernews",
                "title": story.get("title"),
                "url": story.get("url", ""),
                "score": story.get("score", 0),
            })
        if len(results) >= 10:
            break
        time.sleep(0.05)

    return sorted(results, key=lambda x: x["score"], reverse=True)[:5]


def fetch_company_blogs():
    results = []
    for company in COMPANY_FEEDS:
        try:
            feed = feedparser.parse(company["url"])
            for entry in feed.entries[:3]:
                results.append({
                    "source": "company_blog",
                    "company": company["name"],
                    "title": entry.get("title", ""),
                    "url": entry.get("link", ""),
                    "summary": entry.get("summary", "")[:300],
                    "published": entry.get("published", ""),
                })
        except Exception as e:
            print(f"Failed to fetch {company['name']}: {e}", file=sys.stderr)

    return results


def fetch_reddit():
    results = []
    headers = {"User-Agent": "PulsePost/1.0"}
    for subreddit in REDDIT_SUBREDDITS:
        try:
            response = requests.get(
                f"https://www.reddit.com/r/{subreddit}/hot.json?limit=5",
                headers=headers,
                timeout=10,
            ).json()
            for post in response["data"]["children"]:
                data = post["data"]
                results.append({
                    "source": "reddit",
                    "subreddit": subreddit,
                    "title": data["title"],
                    "url": f"https://reddit.com{data['permalink']}",
                    "score": data["score"],
                    "num_comments": data["num_comments"],
                })
        except Exception as e:
            print(f"Failed to fetch r/{subreddit}: {e}", file=sys.stderr)

    return sorted(results, key=lambda x: x["score"], reverse=True)[:5]


def get_transcript(video_id: str) -> str:
    try:
        from youtube_transcript_api import YouTubeTranscriptApi
        transcript = YouTubeTranscriptApi.get_transcript(video_id)
        return " ".join(t["text"] for t in transcript)[:500]
    except Exception:
        return ""


def fetch_youtube():
    results = []
    for channel in YOUTUBE_CHANNELS:
        try:
            feed = feedparser.parse(
                f"https://www.youtube.com/feeds/videos.xml?channel_id={channel['channel_id']}"
            )
            for entry in feed.entries[:2]:
                video_id_match = re.search(r"v=([^&]+)", entry.get("link", ""))
                video_id = video_id_match.group(1) if video_id_match else None

                description = entry.get("summary", "")[:300]
                transcript = get_transcript(video_id) if video_id and not description else ""
                content = transcript if transcript and len(description) < 100 else description

                results.append({
                    "source": "youtube",
                    "channel": channel["name"],
                    "title": entry.get("title", ""),
                    "url": entry.get("link", ""),
                    "content": content,
                    "published": entry.get("published", ""),
                })
        except Exception as e:
            print(f"Failed to fetch YouTube channel {channel['name']}: {e}", file=sys.stderr)

    return results


def fetch_arxiv():
    feed = feedparser.parse("https://rss.arxiv.org/rss/cs.AI")
    return [
        {
            "source": "arxiv",
            "title": entry.title,
            "url": entry.get("link", ""),
            "description": entry.get("summary", "")[:300],
        }
        for entry in feed.entries[:8]
    ]


def fetch_existing_posts():
    response = requests.get(
        "https://api.github.com/repos/Harry-Zhao-AU/harry-zhao-au.github.io/contents/_posts",
        headers=GH_HEADERS,
        timeout=10,
    )
    if response.status_code != 200:
        return []
    return [f["name"].replace(".md", "") for f in response.json() if isinstance(f, dict)]


if __name__ == "__main__":
    print("Fetching Hacker News...", file=sys.stderr)
    hn = fetch_hacker_news()

    print("Fetching company blogs (Anthropic, OpenAI, Google, Meta, Microsoft)...", file=sys.stderr)
    company = fetch_company_blogs()

    print("Fetching Reddit (r/MachineLearning, r/LocalLLaMA)...", file=sys.stderr)
    reddit = fetch_reddit()

    print("Fetching YouTube channels...", file=sys.stderr)
    youtube = fetch_youtube()

    print("Fetching arXiv...", file=sys.stderr)
    arxiv = fetch_arxiv()

    print("Fetching existing posts...", file=sys.stderr)
    existing = fetch_existing_posts()

    output = {
        "hackernews": hn,
        "company_blogs": company,
        "reddit": reddit,
        "youtube": youtube,
        "arxiv": arxiv,
        "existing_posts": existing,
    }

    print(json.dumps(output, indent=2))
