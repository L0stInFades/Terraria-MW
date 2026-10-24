"""Author crisp 2x Terraria sprites from individually studied weapon silhouettes."""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
DATA = json.loads((ROOT / 'Data/arsenal.json').read_text(encoding='utf8'))
ANIMATIONS = json.loads((ROOT / 'Data/animations.json').read_text(encoding='utf8'))
assert {g['Id'] for g in DATA['Guns']} == {a['Id'] for a in ANIMATIONS}
INK = '#101a23'
DARK = '#27333d'
STEEL = '#52636a'
LIGHT = '#95a5a4'
WOOD = '#956140'
TAN = '#b19b6c'

def save(im, path, scale=2):
    p = ROOT / path
    p.parent.mkdir(parents=True, exist_ok=True)
    im.resize((im.width*scale, im.height*scale), Image.Resampling.NEAREST).save(p)

def gun_sprite(g):
    id = g['Id']
    w = 26 if g['Family']=='Pistol' else 66 if g['Family']=='Sniper' else 56 if g['Family']=='LMG' else 50
    im = Image.new('RGBA', (w, 23))
    d = ImageDraw.Draw(im)
    def rect(box, c=STEEL):
        d.rectangle(box, fill=c, outline=INK)
        if box[2]-box[0]>3 and box[3]-box[1]>2:
            d.line((box[0]+1,box[1]+1,box[2]-1,box[1]+1), fill=LIGHT if c==STEEL else '#6b7576' if c==DARK else '#cfb582')
    def poly(points,c=STEEL): d.polygon(points, fill=c, outline=INK)
    def barrel(x0,x1,y=9):
        rect((x0,y,x1,y+2), DARK)
        d.line((x0+1,y,x1-1,y), fill=LIGHT)
    def grip(x,y=12,c=DARK): poly([(x,y),(x+4,y),(x+3,y+8),(x-1,y+8)],c)
    def mag(x,y=12,curved=False,c=DARK):
        poly([(x,y),(x+5,y),(x+4 if curved else x+5,y+5),(x+1 if curved else x+5,y+9),(x-3 if curved else x,y+8),(x if curved else x,y+4)],c)
        d.line((x+1,y+2,x+1,y+6), fill='#768381')
    def scope(x,y=3,length=15):
        rect((x+3,y+4,x+length-3,y+7),DARK)
        rect((x,y,x+length,y+4),DARK)
        rect((x+length-3,y-1,x+length+1,y+5),STEEL)
        d.point((x+length,y+1), fill='#91d6d6')
    def bipod(x,y=12):
        d.line((x,y,x-3,21),fill=INK,width=2)
        d.line((x+1,y,x+5,21),fill=STEEL)
    def rail(x0,x1,y=6):
        for x in range(x0,x1,3):d.rectangle((x,y,x+1,y+1),fill=LIGHT)
    if id in ['USP45','M9','DesertEagle']:
        if id=='USP45':
            rect((5,5,24,10),DARK);rect((7,10,21,13),STEEL)
            poly([(7,12),(13,12),(12,21),(5,21)],DARK)
            rect((13,12,19,16),DARK);d.rectangle((14,13,17,14),fill=(0,0,0,0))
            for x in (7,9,11): d.line((x,6,x,8),fill=LIGHT)
            rect((20,12,22,14),DARK);d.point((22,4),fill=LIGHT);d.point((7,4),fill=LIGHT)
        elif id=='M9':
            barrel(7,25,7);rect((5,5,17,10),STEEL);rect((18,6,22,10),STEEL)
            d.rectangle((10,5,17,7),fill=INK);d.line((11,6,17,6),fill=LIGHT)
            poly([(7,10),(13,10),(12,21),(5,21)],WOOD)
            d.ellipse((12,10,20,16),outline=STEEL);d.line((15,11,14,14),fill=LIGHT)
            d.line((4,5,4,9),fill=LIGHT)
        else:
            rect((4,4,24,10),'#a7afad');poly([(17,4),(25,6),(25,10),(17,10)],'#b9c2bc')
            poly([(7,10),(14,10),(13,22),(4,22)],DARK)
            rect((13,11,20,16),STEEL);d.rectangle((14,12,18,14),fill=(0,0,0,0))
            for x in (6,8,10):d.line((x,5,x,7),fill='#475966')
            d.point((22,3),fill=LIGHT)
    elif id=='MP5':
        poly([(1,8),(12,8),(12,10),(4,10),(4,17),(1,17)],DARK)
        rect((12,7,32,12),DARK);rect((28,8,39,13),STEEL)
        grip(17);mag(25,12,True);barrel(38,48)
        d.ellipse((44,4,48,9),outline=LIGHT);rect((16,5,19,7),STEEL)
    elif id=='UMP45':
        poly([(1,7),(15,7),(15,10),(7,10),(5,17),(1,17)],STEEL)
        d.polygon([(4,10),(10,10),(5,14),(3,14)],fill=(0,0,0,0))
        rect((15,6,36,13),DARK);grip(19);mag(28,12)
        barrel(36,47);rail(18,35,4);rect((35,8,41,12),DARK)
    elif id=='P90':
        poly([(2,6),(37,6),(42,9),(47,9),(47,12),(34,14),(29,20),(8,20),(2,16)],DARK)
        rect((8,3,36,6),'#a99c72');rect((12,6,33,9),STEEL)
        d.ellipse((9,11,17,17),fill=(0,0,0,0),outline=STEEL)
        d.ellipse((26,11,32,17),fill=(0,0,0,0),outline=STEEL)
        for x in range(11,33,3):d.line((x,4,x,5),fill='#665c45')
        barrel(40,48);rect((30,1,35,3),DARK)
    elif id=='Vector':
        poly([(1,6),(15,6),(15,8),(5,9),(4,16),(1,16)],DARK)
        rect((16,5,33,11),STEEL);poly([(23,10),(35,10),(36,20),(25,20)],TAN)
        grip(18,11);mag(21,14);barrel(34,43);rail(17,34,3)
        rect((35,10,40,14),DARK)
    elif id in ['M4A1','M16A4']:
        long=id=='M16A4'
        poly([(1,7),(12,7),(16,10),(13,12),(4,16),(1,16)],DARK)
        if not long:d.rectangle((5,9,10,12),fill=STEEL)
        rect((14,8,28,13),STEEL);rect((28,8,39 if not long else 41,12),DARK)
        grip(17);mag(25,12,True)
        poly([(15,7),(16,3),(27,3),(29,7)],DARK);d.rectangle((18,4,25,5),fill=(0,0,0,0))
        barrel(39,48);d.line((42,9,43,4,44,9),fill=LIGHT)
        for x in range(30,39,2):d.line((x,9,x,11),fill=STEEL)
    elif id=='AK47':
        poly([(1,8),(13,9),(17,11),(13,15),(2,17)],WOOD)
        rect((14,7,31,12),STEEL);rect((31,8,39,13),WOOD)
        barrel(38,49);barrel(32,45,6);d.line((46,8,47,3,48,8),fill=STEEL)
        grip(18,12,WOOD);mag(27,12,True)
        d.line((31,9,38,9),fill='#c18c56');rect((23,5,28,7),DARK)
    elif id=='G36C':
        poly([(1,7),(15,8),(17,11),(6,18),(1,18)],DARK)
        d.polygon([(4,10),(12,10),(5,15)],fill=(0,0,0,0))
        rect((15,7,37,13),STEEL);grip(20);mag(29,12,False,'#756b50')
        poly([(17,6),(19,2),(36,2),(39,6)],DARK);d.rectangle((21,3,34,4),fill=(0,0,0,0))
        rect((36,8,43,13),DARK);barrel(42,48)
        for x in (38,40):d.point((x,10),fill=LIGHT)
    elif id=='FAMAS':
        poly([(1,8),(12,7),(18,9),(37,9),(39,13),(27,14),(21,20),(12,18),(1,17)],DARK)
        mag(7,13);grip(24,12);barrel(35,49)
        poly([(10,8),(12,2),(32,2),(35,8)],STEEL);d.rectangle((15,4,29,6),fill=(0,0,0,0))
        d.line((40,9,42,4,43,9),fill=LIGHT)
    elif id in ['SCARH','ACR']:
        scar=id=='SCARH'
        poly([(1,7),(13,7),(16,10),(11,15),(2,17)],TAN if scar else DARK)
        d.line((3,9,10,9),fill=LIGHT)
        rect((14,6,34,12),TAN if scar else STEEL);rect((33,7,42,12),TAN)
        grip(19,12,TAN if scar else DARK);mag(27,12,not scar)
        barrel(42,49);rail(16,42,4)
        for x in range(34,41,3):d.line((x,9,x+1,9),fill=INK)
        rect((19,2,24,5),DARK)
    elif id in ['M1014','SPAS12']:
        spas=id=='SPAS12'
        poly([(1,7),(14,8),(17,11),(8,16),(2,17),(2,14),(10,11),(1,10)],DARK)
        rect((15,8,30,13),STEEL);grip(18)
        barrel(29,49,7);barrel(27,44,11)
        rect((28,10,40,14),DARK)
        for x in range(30,40,2):d.line((x,11,x,13),fill=STEEL)
        if spas:
            rect((24,4,45,7),STEEL)
            for x in range(26,44,3):d.point((x,5),fill=INK)
            d.line((6,3,29,3),fill=LIGHT,width=2)
    elif id=='AA12':
        poly([(1,6),(13,6),(18,8),(36,8),(41,9),(41,13),(21,14),(12,16),(1,16)],STEEL)
        grip(19);d.ellipse((25,12,36,22),fill=DARK,outline=INK);d.ellipse((28,15,33,20),outline=STEEL)
        barrel(40,49);rail(18,39,6)
    elif id in ['M249','RPD','M60E4']:
        rpd=id=='RPD';m60=id=='M60E4'
        poly([(1,8),(14,9),(17,11),(10,16),(2,17)],WOOD if rpd else DARK)
        rect((15,7,35,13),STEEL);grip(19)
        if rpd:d.ellipse((24,12,35,22),fill=DARK,outline=INK);d.arc((26,14,33,20),0,300,fill=STEEL)
        else:rect((25,12,36,21),'#606849')
        rect((34,8,44,13),WOOD if rpd else DARK);barrel(43,55)
        bipod(46)
        if m60:grip(38,12)
        else:
            for x in range(34,43,3):d.point((x,10),fill=LIGHT)
        if not rpd:
            rect((20,4,31,6),DARK)
            for y in range(9,15,2):d.line((24,y,27,y),fill='#c2a56e')
    elif id in ['M21EBR','MK14']:
        mk=id=='MK14'
        poly([(1,7),(12,7),(17,10),(13,13),(5,18),(1,18)],DARK if mk else STEEL)
        d.rectangle((4,10,10,13),fill=(0,0,0,0))
        rect((15,8,32,13),STEEL);rect((32,9,39,12),DARK)
        grip(19);mag(26,12);barrel(38,49);scope(20,2,10 if mk else 15)
        if not mk:bipod(40)
    elif id=='Intervention':
        poly([(1,8),(15,8),(20,11),(16,15),(5,18),(1,18)],TAN)
        d.polygon([(5,11),(12,11),(6,15)],fill=(0,0,0,0))
        rect((18,9,39,13),STEEL);grip(23);mag(32,13)
        barrel(38,64);scope(23,1,20);bipod(46)
        rect((58,8,64,12),DARK);d.line((19,7,24,7),fill=LIGHT)
    elif id=='BarrettM82':
        poly([(1,8),(16,8),(20,12),(12,17),(2,17)],DARK)
        rect((17,7,42,13),STEEL);grip(22);mag(32,13)
        barrel(41,60);rect((59,6,65,12),STEEL);d.line((61,7,61,11),fill=INK)
        scope(23,0,18);bipod(47)
        for x in range(35,43,3):d.line((x,8,x+1,8),fill=INK)
    else:raise AssertionError(id)
    return im

for g in DATA['Guns']:
    save(gun_sprite(g), 'Assets/Guns/'+g['Id']+'.png')

def action_frame(base, anim, phase):
    image=base.copy()
    amount=[0,.6,1,.7,.25,0][phase]
    if amount==0:
        return image
    d=ImageDraw.Draw(image)
    x,y,w,h=anim['Part']
    assert x>=0 and y>=0 and x+w<=image.width and y+h<=image.height,anim['Id']
    travel=round(anim['Travel']*amount)
    kind=anim['Action']
    if kind=='port':
        d.rectangle((x,y,x+w-1,y+h-1),fill=INK)
        d.rectangle((x+1,y+1,x+max(1,w-2-travel),y+h-2),fill='#a9b3a6')
        d.point((x+w-2,y+1),fill='#e0b66d')
    elif kind=='vector':
        d.rectangle((x,y,x+w-1,y+h-1),fill=DARK,outline=INK)
        d.rectangle((x+2,y+1+travel,x+w-2,min(y+h-1,y+3+travel)),fill=LIGHT,outline=INK)
    elif kind=='belt':
        d.rectangle((x,y,x+w-1,y+h-1),fill=DARK)
        for row in range(y+(phase%2),y+h,2):
            d.line((x+1,row,x+w-2,row),fill='#cfad64')
            d.point((x+1,row),fill='#f2d498')
        d.rectangle((x+w-2,y+1,x+w-1,y+2),fill=INK)
    else:
        part=base.crop((x,y,x+w,y+h))
        d.rectangle((x,y,x+w-1,y+h-1),fill=(0,0,0,0) if kind in ('slide','pump','barrett') else DARK)
        if kind=='slide':
            d.rectangle((x+3,y+2,min(image.width-1,x+w),y+3),fill=LIGHT,outline=INK)
        elif kind=='pump':
            d.line((x,y+1,x+w,y+1),fill=LIGHT)
            d.line((x,y+2,x+w,y+2),fill=DARK)
        image.alpha_composite(part,(x-travel,y))
        d=ImageDraw.Draw(image)
        if kind in ('bolt','cheytac','barrett'):
            ex,ey=anim['Eject']
            d.rectangle((ex-2,ey,ex+2,ey+1),fill=INK)
            d.point((ex+1,ey),fill='#d4b16a')
        if kind=='cheytac':
            # Lift the handle before the bolt reaches the rear of its travel.
            hx=x+5-travel
            d.line((hx,y,hx,y-3 if phase<=2 else y-1),fill=LIGHT)
            d.rectangle((hx-1,y-4 if phase<=2 else y-2,hx+1,y-3 if phase<=2 else y-1),fill=INK)
    return image

def flash_sprite(anim):
    image=Image.new('RGBA',(32,24));d=ImageDraw.Draw(image)
    n=anim['Flash'];length=min(30,10+round(anim['KickPixels']*1.5)+n%3)
    outer=(255,113+n%25,39,210);middle=(255,198+n%20,82,245)
    if anim['Id']=='BarrettM82':
        d.polygon([(0,10),(5,2),(7,9),(15,5),(10,11),(length,12),(10,13),(15,20),(7,15),(5,23),(0,14)],fill=outer)
    elif anim['Casing']==1:
        d.polygon([(0,10),(length-5,5),(length-8,10),(length,12),(length-8,14),(length-5,19),(0,14)],fill=outer)
    elif n%3==0:
        d.polygon([(0,10),(length-6,7),(length-9,11),(length,12),(length-8,14),(length-5,18),(0,14)],fill=outer)
    elif n%3==1:
        d.polygon([(0,10),(length,10),(length-4,12),(length,15),(0,14)],fill=outer)
    else:
        d.polygon([(0,9),(length-4,4),(length-7,10),(length,12),(length-7,14),(length-4,20),(0,15)],fill=outer)
    d.polygon([(0,10),(length-4,12),(0,14)],fill=middle)
    d.line((0,12,max(3,length-9),12),fill='#fff7df',width=2)
    return image

(ROOT/'docs/animations').mkdir(parents=True,exist_ok=True)
animation_rows=[]
for gun,anim in zip(DATA['Guns'],ANIMATIONS):
    assert gun['Id']==anim['Id']
    base=gun_sprite(gun)
    atlas=Image.new('RGBA',(base.width,base.height*6))
    frames=[action_frame(base,anim,phase) for phase in range(6)]
    for phase,frame in enumerate(frames):atlas.paste(frame,(0,base.height*phase))
    save(atlas,'Assets/Animations/'+gun['Id']+'.png')
    flash=flash_sprite(anim)
    save(flash,'Assets/Flashes/'+gun['Id']+'.png')
    assert frames[0].tobytes()==base.tobytes() and any(f.tobytes()!=base.tobytes() for f in frames[1:]),gun['Id']
    preview=[]
    for age in range(anim['Cycle']+14):
        kick=(age+1)/anim['Peak'] if age<anim['Peak'] else max(0,1-(age-anim['Peak'])/(anim['Cycle']-anim['Peak']))**2
        phase=0 if age<anim['ActionDelay'] or age>=anim['ActionDelay']+anim['ActionTicks'] else 1+min(4,(age-anim['ActionDelay'])*5//anim['ActionTicks'])
        layer=Image.new('RGBA',(base.width+42,50))
        layer.alpha_composite(frames[phase],(10-round(kick*anim['KickPixels']/2),13))
        if age<anim['FlashTicks']:
            layer.alpha_composite(flash,(10+anim['Muzzle'][0],13+anim['Muzzle'][1]-12))
        layer=layer.rotate(kick*anim['KickDegrees'],resample=Image.Resampling.NEAREST,center=(20,25))
        card=Image.new('RGBA',layer.size,'#162934');card.alpha_composite(layer)
        preview.append(card.convert('RGB').resize((card.width*4,card.height*4),Image.Resampling.NEAREST))
    preview[0].save(ROOT/'docs/animations'/f"{gun['Id']}.gif",save_all=True,append_images=preview[1:],duration=35,loop=0,optimize=True)
    animation_rows.append(f"| {gun['Name']} | {anim['Notes']} | {anim['KickPixels']} px / {anim['KickDegrees']}° | {anim['Cycle']} 刻 | [动作预览](animations/{gun['Id']}.gif) |")
(ROOT/'docs/ANIMATIONS.md').write_text('# 逐枪持握与开火动作\n\n选中枪械就持续显示，空弹匣、行走和换弹时都保留持握。开火采用逐枪的独立机械帧、后坐曲线、枪口焰和抛壳时间。表中数据均为游戏表现参数；GIF 放慢播放以便观察，实际射速以游戏参数为准。\n\n| 枪械 | 动作设计 | 最大后坐 / 抬枪 | 复位周期 | 预览 |\n| --- | --- | --- | --- | --- |\n'+'\n'.join(animation_rows)+'\n',encoding='utf8')

casings=Image.new('RGBA',(10,18));d=ImageDraw.Draw(casings)
d.rectangle((1,1,8,3),fill='#d4aa58',outline=INK);d.line((2,1,7,1),fill='#f6dd97')
d.rectangle((1,7,8,10),fill='#a25443',outline=INK);d.rectangle((1,7,3,10),fill='#d3b169')
d.rectangle((2,13,6,16),outline='#343d42');d.line((3,13,5,13),fill=LIGHT)
save(casings,'Assets/Casings.png',1)
silencer=Image.new('RGBA',(24,10));d=ImageDraw.Draw(silencer)
d.rectangle((0,2,23,7),fill=DARK,outline=INK);d.line((2,3,21,3),fill=LIGHT)
for x in (3,7,12,17,21):d.line((x,4,x,6),fill=STEEL)
d.line((23,3,23,6),fill='#a9b1a7');save(silencer,'Assets/Suppressor.png',1)
for name,lens in [('Flashlight','#fff1ac'),('Infrared','#ff5151')]:
    device=Image.new('RGBA',(12,6));d=ImageDraw.Draw(device)
    d.rectangle((0,1,10,5),fill=DARK,outline=INK);d.line((2,2,8,2),fill=STEEL)
    d.rectangle((9,2,11,4),fill=lens);save(device,'Assets/'+name+'.png',1)

for a in DATA['Ammo']:
    im=Image.new('RGBA',(11,24));d=ImageDraw.Draw(im)
    shell=a['Caliber']=='12'
    width,height={'9':(3,11),'45':(4,11),'50ae':(5,13),'57':(3,14),'556':(3,18),'762s':(4,17),'762n':(4,20),'408':(4,22),'50bmg':(5,24),'12':(6,16)}[a['Caliber']]
    left=(11-width)//2;right=left+width;top=24-height;neck=top+(3 if a['Caliber'] in ['9','45','50ae'] else 6)
    d.rectangle((left,neck,right,22),fill='#b99354',outline=INK)
    d.line((left+1,neck+1,left+1,21),fill='#f1d5a0')
    if shell:
        d.rectangle((left,top,right,19),fill='#945749' if a['Kind']=='Buckshot' else '#597e61',outline=INK)
        for x in range(left+1,right,2):d.line((x,top+1,x,18),fill='#bb7b68' if a['Kind']=='Buckshot' else '#8bb797')
    else:
        d.polygon([(left,neck),(left+1,top+2),((left+right)//2,top),(right-1,top+2),(right,neck)],fill='#b7b9a4',outline=INK)
        d.rectangle((left+1,top+2,right-1,top+4),fill='#'+a['Color'])
        if a['Kind']=='JHP':d.point(((left+right)//2,top+1),fill=INK)
    d.rectangle((left-1,22,right+1,23),fill='#d3ae67',outline=INK)
    save(im,'Assets/Ammo/'+a['Id']+'.png')

crate=Image.new('RGBA',(48,32));d=ImageDraw.Draw(crate)
d.rectangle((1,5,46,30),fill='#56674a',outline=INK,width=2)
d.rectangle((0,3,47,11),fill='#7e8c62',outline=INK,width=2)
d.line((3,4,44,4),fill='#b6bb85')
for x in (6,37):
    d.rectangle((x,7,x+4,29),fill='#2b3836',outline=INK)
    d.rectangle((x-1,9,x+5,14),fill='#adab7b',outline=INK)
d.rectangle((17,15,31,25),fill='#263d38',outline='#adb587')
d.rectangle((23,17,25,23),fill='#efdb9d');d.rectangle((20,19,28,21),fill='#efdb9d')
for x in (3,44):
    for y in (7,27):d.point((x,y),fill='#a7b797')
save(crate,'Assets/SupplyCrate.png',1)
sheet=Image.new('RGBA',(54,36))
for y in range(2):
    for x in range(3):sheet.paste(crate.crop((x*16,y*16,x*16+16,y*16+16)),(x*18,y*18))
save(sheet,'Assets/SupplyTile.png',1)
round_image=Image.new('RGBA',(4,4));ImageDraw.Draw(round_image).rectangle((0,1,3,2),fill='#ffde9a');save(round_image,'Assets/Round.png',1)
icon=Image.new('RGBA',(80,80),'#152530')
icon.alpha_composite(crate,(16,43))
small=gun_sprite(DATA['Guns'][7]).resize((72,33),Image.Resampling.NEAREST)
icon.alpha_composite(small,(4,9));save(icon,'icon.png',1)

font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',18)
smallfont=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',14)
board=Image.new('RGB',(1440,1180),'#11212c');d=ImageDraw.Draw(board)
d.text((34,25),'MODERN ARSENAL  /  CLASSIC BATTLEFIELDS',fill='#c2e0c1',font=ImageFont.truetype('C:/Windows/Fonts/consolab.ttf',30))
d.text((34,72),'24 individually drawn silhouettes | 2x native pixel grid | 10 calibers / 19 ammunition types',font=font,fill='#91aab5')
for i,g in enumerate(DATA['Guns']):
    x=28+(i%3)*474;y=125+(i//3)*125
    d.rectangle((x,y,x+450,y+113),fill='#1c303b')
    im=gun_sprite(g).resize((gun_sprite(g).width*3,69),Image.Resampling.NEAREST)
    board.paste(im,(x+12,y+14),im)
    d.text((x+220,y+17),g['Name'],font=font,fill='#e2e3cb')
    d.text((x+220,y+44),f"DMG {g['Damage']}  MAG {g['Magazine']}",font=smallfont,fill='#9fbdc3')
    d.text((x+220,y+67),f"{g['Family']}  /  T{g['Tier']}",font=smallfont,fill='#9dcca8')
(ROOT/'docs').mkdir(exist_ok=True)
board.save(ROOT/'docs/arsenal-contact.png')
hashes=[hashlib.sha256((ROOT/('Assets/Guns/'+g['Id']+'.png')).read_bytes()).hexdigest() for g in DATA['Guns']]
assert len(set(hashes))==24
assert len({hashlib.sha256((ROOT/('Assets/Ammo/'+a['Id']+'.png')).read_bytes()).hexdigest() for a in DATA['Ammo']})==19
# CIWS uses the same authored two-pixel grid as the rest of the arsenal.
base=Image.new('RGBA',(40,48));d=ImageDraw.Draw(base)
d.rectangle((13,31,26,44),fill=STEEL,outline=INK)
d.ellipse((11,31,28,36),fill=LIGHT,outline=INK)
d.rectangle((4,43,35,47),fill=DARK,outline=INK)
d.line((5,44,34,44),fill='#bdc4be')
for x in (6,12,27,33):d.rectangle((x,45,x+1,46),fill='#d8be6d')
body=base.resize((80,96),Image.Resampling.NEAREST)
sheet=Image.new('RGBA',(90,108))
for y in range(6):
    for x in range(5):sheet.paste(body.crop((x*16,y*16,x*16+16,y*16+16)),(x*18,y*18))
save(sheet,'Assets/CIWS/Tile.png',1)
head=Image.new('RGBA',(32,34));d=ImageDraw.Draw(head)
d.rectangle((11,13,21,23),fill=DARK,outline=INK)
d.rounded_rectangle((7,0,25,16),radius=6,fill='#b4bdbb',outline=INK,width=1)
d.rounded_rectangle((8,1,23,15),radius=5,fill='#e4e4d7')
d.line((10,3,21,3),fill='#f9f7e5');d.line((9,6,9,13),fill='#f7f3dc')
d.rectangle((11,7,22,13),fill='#c8d0c9')
for x in (12,15,18,21):d.line((x,8,x,12),fill='#829397')
d.polygon([(7,19),(23,17),(28,22),(28,29),(8,29)],fill='#c4c9bf',outline=INK)
d.rectangle((13,20,22,25),fill='#e2e0cc',outline='#667a7d')
d.rectangle((23,24,30,29),fill=DARK,outline=INK)
d.ellipse((1,20,13,32),fill=STEEL,outline=INK)
d.ellipse((3,22,11,30),fill='#a7b7b4',outline=DARK)
d.ellipse((5,24,9,28),fill=DARK)
for y in (22,25,28):d.line((1,y,3,y),fill=LIGHT)
d.rectangle((14,28,21,33),fill=STEEL,outline=INK)
save(head,'Assets/CIWS/Head.png')
barrels=Image.new('RGBA',(34,36))
for frame in range(4):
    d=ImageDraw.Draw(barrels);y=frame*9
    d.rectangle((0,y+2,10,y+7),fill=STEEL,outline=INK)
    d.rectangle((9,y+2,30,y+6),fill=DARK,outline=INK)
    for yy in (2,4,6):d.line((10,y+yy,32,y+yy),fill=LIGHT if (yy+frame)%3 else '#3c535d')
    for x in (15,25):d.rectangle((x,y+1,x+2,y+7),fill=STEEL,outline=INK)
    d.rectangle((30,y+1,33,y+7),fill=DARK,outline=INK)
    for yy in range(2,7,2):d.point((32,y+yy),fill='#e5e1b8' if frame%2 else '#73888e')
save(barrels,'Assets/CIWS/Barrels.png')
preview=Image.new('RGBA',(132,100));preview.alpha_composite(body,(0,4))
preview.alpha_composite(head.resize((64,68),Image.Resampling.NEAREST),(8,6))
preview.alpha_composite(barrels.crop((0,0,34,9)).resize((68,18),Image.Resampling.NEAREST),(32,55))
save(preview.resize((66,50),Image.Resampling.NEAREST),'Assets/CIWS/TurretItem.png')
remote=Image.new('RGBA',(12,18));d=ImageDraw.Draw(remote)
d.line((9,0,9,4),fill=STEEL,width=2)
d.rounded_rectangle((0,3,11,17),radius=2,fill='#34473e',outline=INK)
d.rectangle((2,5,9,9),fill='#10292d',outline='#839b84')
for x,h in [(3,2),(5,3),(7,1)]:d.line((x,8,x,8-h),fill='#72ddbc')
d.rectangle((2,11,5,13),fill='#c67757',outline=INK);d.rectangle((7,11,9,13),fill='#73b9bf',outline=INK)
d.line((3,15,8,15),fill='#9bad92')
save(remote,'Assets/CIWS/Remote.png')
board=Image.new('RGB',(900,380),'#11212c');d=ImageDraw.Draw(board)
d.text((24,18),'PHALANX / 4500 RPM / 75 ROUNDS PER SECOND',font=font,fill='#c2e0c1')
large=preview.resize((528,400),Image.Resampling.NEAREST);board.paste(large,(0,0),large)
rr=remote.resize((120,180),Image.Resampling.NEAREST);board.paste(rr,(680,110),rr)
d.text((550,310),'REMOTE: POWER / AMMO',font=font,fill='#b9d8d0')
board.save(ROOT/'docs/ciws-preview.png')

for lang in ['zh-Hans','en-US']:
    items={g['Id']:{'DisplayName':g['Name'],'Tooltip':''} for g in DATA['Guns']}
    items.update({a['Id']:{'DisplayName':a['Name'],'Tooltip':''} for a in DATA['Ammo']})
    items['SupplyCrate']={'DisplayName':'弹药补给箱' if lang=='zh-Hans' else 'Supply Crate','Tooltip':'右键免费领取弹药与已解锁枪械\n弹药无需合成，弹药箱可挖走搬家' if lang=='zh-Hans' else 'Right-click for free ammunition and unlocked weapons\nNo ammo crafting; mine and relocate the crate'}
    items['PhalanxTurret']={'DisplayName':'密集阵 CIWS 炮塔' if lang=='zh-Hans' else 'Phalanx CIWS Turret','Tooltip':''}
    items['PhalanxRemote']={'DisplayName':'密集阵遥控器' if lang=='zh-Hans' else 'CIWS Remote Controller','Tooltip':''}
    loc={'Mods':{'ModernArsenal':{'Items':items,'Tiles':{'SupplyTile':{'MapEntry':'弹药补给箱' if lang=='zh-Hans' else 'Supply Crate'}},'Keybinds':{'Reload':{'DisplayName':'枪械换弹' if lang=='zh-Hans' else 'Reload firearm'},'ModifyGun':{'DisplayName':'打开改枪界面' if lang=='zh-Hans' else 'Open gunsmith'}}}}}
    loc['Mods']['ModernArsenal']['Tiles']['PhalanxTile']={'MapEntry':'密集阵 CIWS' if lang=='zh-Hans' else 'Phalanx CIWS'}
    path=ROOT/'Localization'/f'{lang}.hjson';path.parent.mkdir(exist_ok=True);path.write_text(json.dumps(loc,ensure_ascii=False,indent=2),encoding='utf8')
print('Generated 24 unique gun sprites, 19 ammo sprites, crate, icon, contact sheet and two localizations.')
