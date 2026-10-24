"""Build the catalog and provenance; local reference photos are optional."""
from pathlib import Path
import json
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
data=json.loads((ROOT/'Data/arsenal.json').read_text(encoding='utf8'))
motions={a['Id']:a for a in json.loads((ROOT/'Data/animations.json').read_text(encoding='utf8'))}
balance_path=ROOT/'artifacts/balance-results.json'
if not balance_path.exists(): balance_path=ROOT/'docs/verification/balance-1.2.0.json'
balance=json.loads(balance_path.read_text(encoding='utf8'))
assert balance['success']
measured={g['id']:g for g in balance['modern']}
stages=['开局','克眼后','邪恶首领或骷髅王后','困难模式','任一机械首领后','世纪之花后']
for gun in data['Guns']:
    gun['Motion']=motions[gun['Id']]
    gun['Sustain']=measured[gun['Id']]['sustainDps']
    gun['Cycle']=measured[gun['Id']]['cycleDps']
    assert gun['Damage']==measured[gun['Id']]['damage'], 'Rerun the balance audit after changing weapon damage'
sources=json.loads((ROOT/'docs/reference/sources.json').read_text(encoding='utf8'))
assert len(sources)==len(data['Guns'])==24
studies=ROOT/'docs/pixel-studies'
studies.mkdir(exist_ok=True)
rows=[]
for g, source in zip(data['Guns'],sources):
    assert g['Id']==source['id']
    g['ReferenceImage']=source['image']
    g['ReferencePage']=source['page']
    path=ROOT/'docs/reference'/f"{g['Id']}.jpg"
    if path.exists():
        im=Image.open(path).convert('RGB')
        im.thumbnail((96,48),Image.Resampling.LANCZOS)
        im=im.quantize(colors=16).convert('RGB')
        im.resize((im.width*5,im.height*5),Image.Resampling.NEAREST).save(studies/f"{g['Id']}.png")
    rows.append(f"| {g['Name']} | [参考图]({source['image']}) / [出处]({source['page']}) | {g['Detail']} | [原创成品](../Assets/Guns/{g['Id']}.png) |")
references='''# 图片参考与像素轮廓研究

检索日期：2026-10-24。先检索图片，再逐张检查枪型和视角；去掉了搜索结果中的无关图片及误标的 1911、G36 等图像。可核对的最终 24 项来源保存在 `reference/sources.json`。仓库保留出处链接，参考照片、网页抓取和由照片派生的研究图留在本机，不随源码上传。

研究图先缩至最多 96×48，量化为 16 色，再最近邻放大。游戏成品依据这些参考的枪托、机匣、弹匣、提把、导轨、瞄具、脚架和枪口轮廓独立绘制，使用 2 像素网格；24 把枪有不同轮廓和独立贴图。照片缩小图没有直接作为物品素材。

第三方参考图片与像素化研究仅供本地设计核对，版权归各来源所有，未打入 `.tmod`。游戏内枪械、弹药、补给箱和图标由本项目的 `tools/generate_assets.py` 绘制。M21 EBR 另参考同平台的 M14 EBR 底盘，AA-12 的圆鼓、狙击瞄具和部分配色按经典游戏视觉补全。

| 枪械 | 来源 | 轮廓观察 | 原创成品 |
| --- | --- | --- | --- |
'''+ '\n'.join(rows)+'\n'
(ROOT/'docs/REFERENCES.md').write_text(references,encoding='utf8')
comparison=[]
for gun in data['Guns']:
    native=max((g for g in balance['vanilla'] if g['tier']==gun['Tier']),key=lambda g:g['sustainDps'])
    ratio=min(gun['Sustain'],gun['Cycle'])/native['sustainDps']
    assert ratio>=1.1,(gun['Id'],ratio)
    comparison.append(f"| {gun['Name']} | {stages[gun['Tier']]} | {gun['Damage']} | {gun['Magazine']} / {gun['Reload']/60:.2f}s | {gun['Sustain']:.0f} | {gun['Cycle']:.0f} | {native['weapon']} / {native['sustainDps']:.0f} | ×{ratio:.2f} |")
native_rows=[f"| {g['weapon']} | {g['ammo']} | {g['damage']} | {g['useTime']} | {g['bullets']} | {g['sustainDps']:.2f} |" for g in balance['vanilla']]
(ROOT/'docs/BALANCE.md').write_text('''# 1.2 全枪伤害上调与持续输出

24 把枪的基础伤害已整体提高，困难模式至后期按强化连战的百万血量目标重做。Vector 使用普通 FMJ 的完整弹匣长期循环约 9 万 / 秒，Barrett 约 30 万 / 秒。满血、护甲、射程与故意失误的原生碰撞对照，以及论坛资料见 [1.2 调整记录](REBALANCE_1.2.md)。本页保留原版参照，便于核对整体提升幅度。

读取本机 tModLoader 2026.8.3 / Terraria 1.4.4.9 的 `Terraria.Item`、`Terraria.Player.ItemCheck_Shoot` 和 `Terraria.Projectile`，再运行 1800 刻（30 秒）的原生 `Player.ItemCheck` 与控制弹 AI。现代枪械从满弹匣开始，储备弹药充足，完整经历实际的打空、换弹停火、装填和继续射击；三连发的休止也由原生循环处理。弹药使用各枪当前解锁阶段可用的高伤害兼容弹。

统一不穿装备、不加前缀。统计发射伤害并按原生暴击概率求期望。命中模型取全命中，不计敌人护甲、走位、地形和远距衰减，不等于首领实战 DPS。对原版水晶碎弹、神圣箭落星、幻象追击箭与月明箭回波采用代码给出的完整二次伤害上限，因此对照刻意偏向原版。原始报告见 `artifacts/balance-results.json`，测试代码为 `Verification/ArsenalBalanceAudit.cs`。

不能直接用物品 `useTime` 推算所有原版武器：星旋机枪的 615 号控制弹每 5 刻发射子弹，每七次另发一枚伤害增加 20 的火箭；幻象的 630 号控制弹从 24 刻一轮加速到 18 刻一轮，每轮四箭。幻象命中还会生成三枚 30% 伤害的追击箭。源码摘录与 ID 核对保存在 `docs/vanilla-weapon-code.json`，完整反编译仅在 `artifacts/` 留作本机核对，不打入模组。

另外计算无限重复完整弹匣的长期输出：整匣期望伤害 ÷（整匣发射间隔总和 + 三连发休止总和 + 换弹时间）。它计入每一匣后的完整换弹，排除 30 秒采样从满弹匣开始、在半匣结束的优势。这个保守计算不计穿透多个敌人或 JHP 对低防目标的额外增伤。

下表是当前版本的同阶段对照。每把现代枪械的 30 秒原生采样和完整弹匣长期循环都高于对应阶段所选原版参考的最高值至少 10%，比值取两项结果中较低的一项。每一阶段只与该阶段列出的参考比较，不声称覆盖所有原版流派、装备组合和多目标情形。

| 枪械 | 开放阶段 | 本体伤害 | 弹匣 / 换弹 | 30 秒原生采样 / 秒 | 完整弹匣长期循环 / 秒 | 同阶段参考最高值 | 较低项比值 |
| --- | --- | --- | --- | --- | --- | --- | --- |
'''+ '\n'.join(comparison)+'''

## 原版采样

| 武器 | 弹药 | 本体伤害 | 物品使用间隔 / 刻 | 发射弹丸数 | 完整伤害上限期望 / 秒 |
| --- | --- | --- | --- | --- | --- |
'''+ '\n'.join(native_rows)+'''

短弹匣和长换弹通过提高单发、暴击或穿甲补偿。近战射程的霰弹与远距独头弹各有用途；消音器仅改变声音与枪口焰，不额外扣伤害。射手装备和前缀继续通过原生伤害与暴击计算生效。测试没有代替完整生存通关或所有首领的实战比较。
''',encoding='utf8')

page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>现代军械库 · 经典战场</title>
<style>
:root{color-scheme:dark;--bg:#101b24;--panel:#1a2d38;--line:#38505b;--green:#a5d4ac;--gold:#dac38a}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:#e0e5e0;font:16px/1.65 system-ui,"Microsoft YaHei",sans-serif}main{max-width:1350px;margin:auto;padding:52px 28px}a{color:var(--green)}header{border-bottom:1px solid var(--line);padding-bottom:28px}.tag{letter-spacing:.22em;color:var(--green);font-size:12px}h1{font-size:clamp(30px,4vw,58px);margin:8px 0}h2{font-size:27px;margin:40px 0 14px}p{color:#a8bdc5;max-width:960px}.facts{display:flex;gap:34px;flex-wrap:wrap}.facts b{font:38px/1.2 ui-monospace,monospace;color:var(--gold)}.facts span{display:block;color:#9db3bc;font-size:13px}.controls{display:flex;gap:12px;flex-wrap:wrap;margin:28px 0 20px}input,select{background:#203642;color:#f2f0e1;border:1px solid #4c6b72;border-radius:5px;padding:10px;font:inherit}input{min-width:220px}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(350px,1fr));gap:18px}.card{background:var(--panel);padding:20px;border:1px solid #2b434e;border-top:3px solid var(--green)}.card h3{font-size:22px;margin:0 0 4px}.meta{color:var(--green);font-size:13px}.gun{height:92px;display:flex;align-items:center;justify-content:center;background:#12212b;margin:14px 0}.gun img{image-rendering:pixelated;max-width:95%;max-height:80px;transform:scale(1.12)}.stats{display:grid;grid-template-columns:repeat(3,1fr);gap:6px;font:20px ui-monospace,monospace}.stats small{display:block;font:12px/1.8 system-ui;color:#a0b5c0}.detail{font-size:13px;color:#aebfc6;min-height:45px}.reference{display:flex;gap:10px;align-items:center;padding-top:10px;border-top:1px solid #36505b}.reference img{width:46%;max-height:110px;object-fit:contain;background:#d2d5cf}.reference .pixels{image-rendering:pixelated}.scroll{overflow:auto}table{width:100%;border-collapse:collapse;font-size:14px;white-space:nowrap}td,th{text-align:left;border-bottom:1px solid #304752;padding:11px}th{color:var(--green)}td:first-child{color:var(--gold)}details{margin-top:32px}summary{cursor:pointer;color:var(--green)}footer{margin-top:46px;padding-top:22px;border-top:1px solid var(--line);color:#94aeb7;font-size:13px}.notice{border-left:3px solid var(--gold);padding:10px 18px;background:#192b34}.snapshot{width:100%;max-width:1000px;border:1px solid var(--line);margin:12px 0}@media(max-width:500px){main{padding:25px 14px}.grid{grid-template-columns:1fr}.stats{font-size:18px}}
</style><main><header><div class="tag">MODERN ARSENAL / CLASSIC BATTLEFIELDS</div><h1>现代军械库 · 经典战场</h1><p>以使命召唤经典现代枪械组成一套高伤害生存军械体系。开局 USP、家中免费领弹，从肉前一路推进到后期重火力。下列数值和游戏内容使用同一份目录。</p><div class="facts"><div><b>24</b><span>独立枪械轮廓</span></div><div><b>10 / 19</b><span>口径 / 弹药种类</span></div><div><b>65</b><span>USP + FMJ 基础伤害</span></div><div><b>600</b><span>每次免费领取弹药</span></div></div></header>
<h2>装备与进度</h2><p>新兵 → 克眼 → 邪恶首领或骷髅王 → 困难模式 → 任一机械首领 → 世纪之花。枪械与弹药均从补给箱按进度免费领取。R 换弹；弹药栏优先；切枪取消装填。用完弹匣后可改装其他兼容弹种。</p><div class="controls"><label>查找 <input id="search" placeholder="枪名、枪种或口径"></label><label>进度 <select id="tier"><option value="-1">显示全部阶段</option></select></label><label>类别 <select id="family"><option value="">显示全部枪种</option></select></label></div><div id="count" class="meta"></div><div id="guns" class="grid"></div>
<h2>弹药与弹道</h2><p>高速弹丸每刻进行 8 次移动与碰撞。存在重力、水平速度损失、射程与远距伤害衰减。穿透只作用于目标，墙壁阻挡所有弹种；每穿透一名敌人，剩余伤害降低 15%。弹道数值采用游戏单位。</p><div class="notice">AP：穿甲与贯穿　JHP：低防目标增伤　Tracer：亮色曳光　Match：低下坠远射　Buckshot：8～9 粒霰弹　Slug：单发重弹，约保留全霰弹命中伤害的 78%</div><div class="scroll"><table><thead><tr><th>弹种</th><th>伤害</th><th>穿甲</th><th>命中目标</th><th>射程 / 格</th><th>下坠</th><th>水平保速 / 刻</th><th>解锁</th></tr></thead><tbody id="ammo"></tbody></table></div>
<h2>实际游戏画面</h2><p>客户端内运行的真实补给界面与射击流程。完整验证记录保存在 artifacts 目录。</p><img class="snapshot" src="screenshots/armory.png" alt="游戏内军械补给界面"><img class="snapshot" src="screenshots/usp.png" alt="USP 在正常游戏画面中射击僵尸">
<footer>本地构建：Terraria 1.4.4.9 / tModLoader 2026.8.3。参考图片保留原来源版权，仅用于轮廓研究；游戏内像素素材独立绘制，第三方图片不打入模组。<br><a href="REFERENCES.md">24 项图片来源与像素化研究</a> · <a href="../README.md">安装、操作及验证说明</a> · <a href="../启动现代军械库.bat">启动现代军械库</a></footer></main>
<script>
const data=DATA_JSON;
const stages=['开局','克眼后','邪恶首领或骷髅王后','困难模式','任一机械首领后','世纪之花后'];
const families={Pistol:'手枪',SMG:'冲锋枪',Rifle:'突击步枪',Shotgun:'霰弹枪',LMG:'轻机枪',Marksman:'精确射手',Sniper:'狙击步枪'};
const byId=id=>document.getElementById(id);
stages.forEach((v,i)=>byId('tier').add(new Option(v,i)));
Object.entries(families).forEach(([k,v])=>byId('family').add(new Option(v,k)));
function render(){const q=byId('search').value.trim().toLowerCase(),tier=Number(byId('tier').value),family=byId('family').value;const guns=data.Guns.filter(g=>(tier<0||g.Tier<=tier)&&(!family||g.Family===family)&&(g.Name+' '+families[g.Family]+' '+g.Caliber).toLowerCase().includes(q));byId('count').textContent=`显示 ${guns.length} / 24 把枪 · 原创像素素材与出处链接；外部照片按原来源加载`;byId('guns').innerHTML=guns.map(g=>`<article class="card"><h3>${g.Name}</h3><div class="meta">${families[g.Family]} · ${stages[g.Tier]}</div><div class="gun"><img src="../Assets/Guns/${g.Id}.png" alt="${g.Name} 游戏像素贴图"></div><div class="stats"><div>${g.Damage}<small>本体伤害${g.Pellets>1?' / 每粒':''}</small></div><div>${g.Magazine}<small>弹匣 / 发</small></div><div>${(g.Reload/60).toFixed(2)}s<small>换弹时间</small></div></div><p class="detail">${g.Burst===3?'三连发':'持续射击'} · ${g.Pellets>1?g.Pellets+' 粒霰弹 / 发 · ':''}${g.Speed} 像素 / 刻 · ${g.Spread}° 散布<br>${g.Detail}</p><div class="reference"><img src="${g.ReferenceImage}" alt="${g.Name} 外部参考图片" loading="lazy"><a href="${g.ReferencePage}" target="_blank" rel="noopener noreferrer">参考来源</a></div></article>`).join('');}
['search','tier','family'].forEach(id=>byId(id).addEventListener('input',render));render();
byId('ammo').innerHTML=data.Ammo.map(a=>`<tr><td>${a.Name}</td><td>+${a.Damage}</td><td>${a.Armor}</td><td>${a.Penetrate}</td><td>${(a.Range/16).toFixed(1)}</td><td>${a.Gravity}</td><td>${(a.Drag*100).toFixed(2)}%</td><td>${stages[a.Tier]}</td></tr>`).join('');
console.assert(data.Guns.length===24&&data.Ammo.length===19&&new Set(data.Ammo.map(a=>a.Caliber)).size===10);
</script></html>'''
page=page.replace('DATA_JSON',json.dumps(data,ensure_ascii=False).replace('</','<\\/'))
usp=next(g for g in data['Guns'] if g['Id']=='USP45')
fmj=next(a for a in data['Ammo'] if a['Id']=='FortyFiveFMJ')
page=page.replace('<b>65</b>',f"<b>{usp['Damage']+fmj['Damage']}</b>")
page=page.replace('<h2>装备与进度</h2>','<p><a href="ANIMATIONS.md">24 把枪的独立动作</a> · <a href="BALANCE.md">计入换弹的数值对照</a> · <a href="ATTACHMENTS.md">改件实物资料与操作</a></p><h2>装备与进度</h2>')
page=page.replace('${g.Speed} 像素 / 刻', '${g.Sustain.toFixed(0)} / ${g.Cycle.toFixed(0)} 输出（30 秒 / 完整弹匣长期循环，均含换弹）<br>${g.Speed} 像素 / 刻')
page=page.replace('<div class="reference"><img src="${g.ReferenceImage}"', '<img src="animations/${g.Id}.gif" alt="${g.Name} 独立开火动作慢放" loading="lazy" style="width:100%;image-rendering:pixelated"><p class="detail">${g.Motion.Notes}</p><div class="reference"><img src="${g.ReferenceImage}"')
(ROOT/'docs/军械图鉴.html').write_text(page,encoding='utf8')
assert page.count('DATA_JSON')==0
print('Created the interactive catalog, optional local pixel studies, and reference provenance.')
