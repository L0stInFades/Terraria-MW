"""Retain web image search evidence and contact sheets for silhouette studies."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import html
import io
import json
import re
import urllib.parse
import urllib.request
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs/reference'
OUT.mkdir(parents=True, exist_ok=True)
CACHE = ROOT / 'artifacts/research-cache'
CACHE.mkdir(parents=True, exist_ok=True)
DATA = json.loads((ROOT / 'Data/arsenal.json').read_text(encoding='utf8'))
QUERIES = ['USP手枪','伯莱塔92F','沙漠之鹰','MP5','UMP冲锋枪','P90','Vector冲锋枪','M4A1','AK47','M16A4','G36C','FAMAS','SCAR-H','ACR步枪','M1014','SPAS-12','AA-12','M249','RPD轻机枪','M60机枪','M21狙击步枪','MK14','CheyTac','巴雷特']
OVERRIDES = {
    'DesertEagle': ('https://modernfirearms.net/userfiles/_thumbs/Images/handguns/israel/1287736449.jpg', 'https://modernfirearms.net/en/handguns/handguns-en/israel-semi-automatic-pistols/desert-eagle-eng/', 'Desert Eagle: thick slide and polygonal barrel'),
    'FAMAS': ('https://modernfirearms.net/wp-content/uploads/2010/10/famasf11-650x227.jpg', 'https://modernfirearms.net/en/assault-rifles/france-assault-rifles/famas-eng/', 'FAMAS F1: carry handle and bullpup layout'),
    'RPD': ('https://modernfirearms.net/userfiles/_thumbs/Images/machine/mg14/rpd.jpg', 'https://modernfirearms.net/en/machineguns/russia-machineguns/rpd-eng/', 'RPD: wood stock, belt drum and bipod'),
    'M60E4': ('https://modernfirearms.net/userfiles/_thumbs/Images/machine/mg12/m60e4.jpg', 'https://modernfirearms.net/en/machineguns/u-s-a-machineguns/m60-eng/', 'M60E4: foregrip, handguard and belt box'),
    'M9': ('https://i.guancha.cn/bbs/2020/05/27/20200527144820243.png?imageView2/2/w/500/format/png', 'https://user.guancha.cn/main/content?id=316383', 'Beretta M9: open slide and barrel'),
    'M16A4': ('https://game.gtimg.cn/images/gp/zlkdatasys/qiangxie/image/20201104/e7d6f62b1c65bfb5f38c62008288bbbd.png', 'https://gp.qq.com/', 'Tencent M16A4 reference render'),
    'ACR': ('https://modernfirearms.net/userfiles/_thumbs/Images/civil/civ018/masada-5.jpg', 'https://modernfirearms.net/en/assault-rifles/u-s-a-assault-rifles/magpul-masada-acr-eng/', 'Magpul MASADA / ACR reference'),
    'SPAS12': ('https://static.wixstatic.com/media/0cdaa4_54d06f934de84e83a715229f5aef0ef8~mv2_d_5183_2522_s_4_2.jpg', 'https://spas-12.com/', 'SPAS-12 Project collection photograph'),
    'M21EBR': ('https://pic.baike.soso.com/ugc/baikepic2/2253/20200619052038-1898111842_jpeg_500_308_26317.jpg/0', 'https://baike.sogou.com/v666164.htm', 'M14 EBR platform: chassis and scope study'),
    'Intervention': ('https://shp.qpic.cn/cfwebcap/0/ccdba7bbc962248bd635f8ad04c1d83b/0/?width=1268&height=585', 'https://codm.qq.com/', 'COD Mobile M200 / Intervention reference'),
}
ump = next(s for s in json.loads((OUT/'sources.json').read_text(encoding='utf8')) if s['id']=='UMP45')
OVERRIDES['UMP45'] = (ump['image'], 'https://github.com/ByMykel/CSGO-API', 'UMP-45 reference render; silhouette only')

def fetch(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'}), timeout=18) as r:
        return r.read()

def research(gun):
    target = OUT / (gun['Id'] + '.jpg')
    manifest = OUT / (gun['Id'] + '.json')
    query = QUERIES[DATA['Guns'].index(gun)]
    if gun['Id'] in OVERRIDES:
        url, page, title = OVERRIDES[gun['Id']]
        image = Image.open(io.BytesIO(fetch(url))).convert('RGB')
        image.thumbnail((1600,1000))
        image.save(target)
        record = {'id':gun['Id'], 'query':query, 'search':None, 'image':url, 'page':page, 'title':title, 'observations':gun['Detail']}
        manifest.write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf8')
        print(gun['Id'], image.size, url, flush=True)
        return record
    if target.exists() and manifest.exists():
        record = json.loads(manifest.read_text(encoding='utf8'))
        if record['query'] == query and 'setlang=zh-Hans' in record['search']:
            return record
    url = 'https://www.bing.com/images/search?q=' + urllib.parse.quote(query) + '&setlang=zh-Hans'
    page = fetch(url).decode('utf8', 'replace')
    (CACHE / (gun['Id'] + '-search.html')).write_text(page, encoding='utf8')
    candidates = []
    for raw in re.findall(r'\bm="([^"]+)"', page):
        try:
            obj = json.loads(html.unescape(raw))
            if 'murl' in obj and 'turl' in obj:
                candidates.append(obj)
        except (ValueError, TypeError):
            continue
    if not candidates:
        raise RuntimeError('No image result metadata for ' + gun['Id'])
    for obj in candidates[:8]:
        try:
            image = Image.open(io.BytesIO(fetch(obj['turl']))).convert('RGB')
            if image.width < 140 or image.height < 55:
                continue
            image.save(target)
            record = {'id':gun['Id'], 'query':query, 'search':url, 'image':obj['murl'], 'page':obj.get('purl'), 'thumbnail':obj['turl'], 'title':obj.get('t'), 'observations':gun['Detail']}
            manifest.write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding='utf8')
            print(gun['Id'], image.size, obj['murl'], flush=True)
            return record
        except Exception as ex:
            print(gun['Id'], type(ex).__name__, str(ex)[:100], flush=True)
    raise RuntimeError('All thumbnails failed: ' + gun['Id'])

with ThreadPoolExecutor(max_workers=6) as pool:
    records = list(pool.map(research, DATA['Guns']))
(OUT / 'sources.json').write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding='utf8')
font = ImageFont.truetype('C:/Windows/Fonts/consola.ttf', 17)
for group in range(3):
    board = Image.new('RGB', (1280, 800), '#edf0ed')
    draw = ImageDraw.Draw(board)
    for n, gun in enumerate(DATA['Guns'][group*8:(group+1)*8]):
        x, y = (n % 2)*640, (n // 2)*200
        im = Image.open(OUT / (gun['Id']+'.jpg'))
        im.thumbnail((600, 155))
        board.paste(im, (x+(640-im.width)//2, y+32+(155-im.height)//2))
        draw.text((x+16,y+5), gun['Name'], font=font, fill='#172629')
    board.save(OUT / f'contact-{group+1}.jpg')
assert len(records) == 24
