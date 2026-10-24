# 图片参考与像素轮廓研究

检索日期：2026-10-24。先检索图片，再逐张检查枪型和视角；去掉了搜索结果中的无关图片及误标的 1911、G36 等图像。可核对的最终 24 项来源保存在 `reference/sources.json`。仓库保留出处链接，参考照片、网页抓取和由照片派生的研究图留在本机，不随源码上传。

研究图先缩至最多 96×48，量化为 16 色，再最近邻放大。游戏成品依据这些参考的枪托、机匣、弹匣、提把、导轨、瞄具、脚架和枪口轮廓独立绘制，使用 2 像素网格；24 把枪有不同轮廓和独立贴图。照片缩小图没有直接作为物品素材。

第三方参考图片与像素化研究仅供本地设计核对，版权归各来源所有，未打入 `.tmod`。游戏内枪械、弹药、补给箱和图标由本项目的 `tools/generate_assets.py` 绘制。M21 EBR 另参考同平台的 M14 EBR 底盘，AA-12 的圆鼓、狙击瞄具和部分配色按经典游戏视觉补全。

| 枪械 | 来源 | 轮廓观察 | 原创成品 |
| --- | --- | --- | --- |
| USP .45 | [参考图](http://x0.ifengimg.com/res/2019/0BA62B61C77273DF346699C400BA199B2D11FEE3_size240_w1000_h750.jpeg) / [出处](https://ishare.ifeng.com/c/s/7r2h2wy99x8) | 方形套筒、外露击锤、前端导轨；开局主力 | [原创成品](../Assets/Guns/USP45.png) |
| M9 | [参考图](https://i.guancha.cn/bbs/2020/05/27/20200527144820243.png?imageView2/2/w/500/format/png) / [出处](https://user.guancha.cn/main/content?id=316383) | 开放式套筒、圆形扳机护圈、长直枪管 | [原创成品](../Assets/Guns/M9.png) |
| Desert Eagle | [参考图](https://modernfirearms.net/userfiles/_thumbs/Images/handguns/israel/1287736449.jpg) / [出处](https://modernfirearms.net/en/handguns/handguns-en/israel-semi-automatic-pistols/desert-eagle-eng/) | 厚重银色套筒、多边形枪管、宽握把 | [原创成品](../Assets/Guns/DesertEagle.png) |
| MP5 | [参考图](https://shp.qpic.cn/inewsnopic/326307006/bd4b614731f49a0e72a0ff80dbb793b5/0) / [出处](https://wefire.qq.com/webplat/info/news_version3/9740/10945/10948/11547/11553/m8580/201512/408400.shtml) | 环形准星、弧形弹匣、伸缩枪托 | [原创成品](../Assets/Guns/MP5.png) |
| UMP45 | [参考图](https://community.akamai.steamstatic.com/economy/image/i0CoZ81Ui0m-9KwlBY1L_18myuGuq1wfhWSaZgMttyVfPaERSR0Wqmu7LAocGIGz3UqlXOLrxM-vMGmW8VNxu5Dx60noTyLkk4a0qB1a7s2rZqNkLvWsHmKCx-FkveBnTCyMmRQguynLwo2reHmVbgQkApR1FrNc50KwmoazMbyw4VHeioJBziX-hiocvH1otvFCD_SLOTiQGw) / [出处](https://github.com/ByMykel/CSGO-API) | 方形镂空折叠托、直弹匣、聚合物机匣 | [原创成品](../Assets/Guns/UMP45.png) |
| P90 | [参考图](http://n.sinaimg.cn/sinacn20191103ac/736/w480h256/20191103/bdcd-ihuuxuu4373521.jpg) / [出处](https://k.sina.com.cn/article_6433656384_17f79ce4000100pc1q.html) | 上置半透明弹匣、无托布局、双握孔 | [原创成品](../Assets/Guns/P90.png) |
| Vector | [参考图](http://game.gtimg.cn/images/cfhd/cp/a20180523cfhd/weapon/KRISS-b.png) / [出处](http://cfhd.cf.qq.com/cp/a20180523cfhd/index.htm) | 前下方大型机匣、短枪管、直弹匣 | [原创成品](../Assets/Guns/Vector.png) |
| M4A1 | [参考图](https://shp.qpic.cn/inewsnopic/326307006/784cc3f8b32c32e4eb5f4f0f79969f6a/0) / [出处](https://wefire.qq.com/webplat/info/news_version3/9740/10945/10948/11547/11548/m8580/201512/407823.shtml) | 伸缩枪托、提把导轨、三角准星、弧形弹匣 | [原创成品](../Assets/Guns/M4A1.png) |
| AK-47 | [参考图](http://n.sinaimg.cn/sinakd20200604ac/605/w1080h325/20200604/2cbc-iumkapx1216055.jpg) / [出处](https://k.sina.cn/article_5921537440_160f37da000100puc3.html) | 木质枪托护木、大弧度弹匣、长导气管 | [原创成品](../Assets/Guns/AK47.png) |
| M16A4 | [参考图](https://game.gtimg.cn/images/gp/zlkdatasys/qiangxie/image/20201104/e7d6f62b1c65bfb5f38c62008288bbbd.png) / [出处](https://gp.qq.com/) | 固定枪托、长护木、提把；三连发 | [原创成品](../Assets/Guns/M16A4.png) |
| G36C | [参考图](http://n.sinaimg.cn/sinakd10015/700/w960h540/20201004/14d3-kaaxtfm6999413.jpg) / [出处](https://k.sina.com.cn/article_6536940867_m185a1cd4300100wll0.html) | 镂空折叠托、长提把、短护木 | [原创成品](../Assets/Guns/G36C.png) |
| FAMAS | [参考图](https://modernfirearms.net/wp-content/uploads/2010/10/famasf11-650x227.jpg) / [出处](https://modernfirearms.net/en/assault-rifles/france-assault-rifles/famas-eng/) | 高架提把、后置弹匣、长直枪管；三连发 | [原创成品](../Assets/Guns/FAMAS.png) |
| SCAR-H | [参考图](https://fabu.ccynice.com/upload/0d3b47fe45b460aa9dab43734a9fa624.jpg) / [出处](https://sy.ccynice.com/shiyong/908.html) | 沙色机身、折叠伸缩托、粗直弹匣 | [原创成品](../Assets/Guns/SCARH.png) |
| ACR | [参考图](https://modernfirearms.net/userfiles/_thumbs/Images/civil/civ018/masada-5.jpg) / [出处](https://modernfirearms.net/en/assault-rifles/u-s-a-assault-rifles/magpul-masada-acr-eng/) | 连续顶导轨、平直护木、折叠托；精度主力 | [原创成品](../Assets/Guns/ACR.png) |
| M1014 | [参考图](http://d.ifengimg.com/q100/img1.ugc.ifeng.com/newugc/20190805/22/wemedia/afee9c289231c22814f2e1f5ddfa4fc386baf9ac_size949_w1104_h618.png) / [出处](https://mil.ifeng.com/c/7otdy65GULY) | 伸缩骨架托、粗枪管、短管式弹仓 | [原创成品](../Assets/Guns/M1014.png) |
| SPAS-12 | [参考图](https://static.wixstatic.com/media/0cdaa4_54d06f934de84e83a715229f5aef0ef8~mv2_d_5183_2522_s_4_2.jpg) / [出处](https://spas-12.com/) | 带孔隔热罩、粗泵动护木、折叠托 | [原创成品](../Assets/Guns/SPAS12.png) |
| AA-12 | [参考图](https://bkimg.cdn.bcebos.com/pic/11794d43ca68af609213c628?x-bce-process=image/format,f_auto/resize,m_lfit,limit_1,h_193) / [出处](https://baike.baidu.com/item/艾奇逊AA-12自动霰弹枪/6184887) | 圆鼓弹匣、厚实聚合物机身；全自动霰弹 | [原创成品](../Assets/Guns/AA12.png) |
| M249 SAW | [参考图](https://game.gtimg.cn/images/gp/zlkdatasys/qiangxiem/image/20201104/93eb5b3ffdd7aa3380d7bee6f81392ee.png) / [出处](https://gp.qq.com/zlkdatasys/qiangxiem/m249.html) | 大型弹链盒、通风护木、折叠两脚架 | [原创成品](../Assets/Guns/M249.png) |
| RPD | [参考图](https://modernfirearms.net/userfiles/_thumbs/Images/machine/mg14/rpd.jpg) / [出处](https://modernfirearms.net/en/machineguns/russia-machineguns/rpd-eng/) | 木托、圆形弹链鼓、长枪管与脚架 | [原创成品](../Assets/Guns/RPD.png) |
| M60E4 | [参考图](https://modernfirearms.net/userfiles/_thumbs/Images/machine/mg12/m60e4.jpg) / [出处](https://modernfirearms.net/en/machineguns/u-s-a-machineguns/m60-eng/) | 宽弹链箱、前垂直握把、粗长枪管 | [原创成品](../Assets/Guns/M60E4.png) |
| M21 EBR | [参考图](https://pic.baike.soso.com/ugc/baikepic2/2253/20200619052038-1898111842_jpeg_500_308_26317.jpg/0) / [出处](https://baike.sogou.com/v666164.htm) | 骨架底盘、长瞄具、直弹匣；半自动精确射击 | [原创成品](../Assets/Guns/M21EBR.png) |
| MK14 | [参考图](https://game.gtimg.cn/images/gp/zlkdatasys/qiangxie/image/20200428/2b873301f6a0fb2f5132e7fc0af263ea.png) / [出处](https://gp.qq.com/zlkdatasys/qiangxie/mk14.html) | M14 轮廓、现代伸缩托、短瞄准镜 | [原创成品](../Assets/Guns/MK14.png) |
| Intervention | [参考图](https://shp.qpic.cn/cfwebcap/0/ccdba7bbc962248bd635f8ad04c1d83b/0/?width=1268&height=585) / [出处](https://codm.qq.com/) | 大型高架瞄具、镂空可调托、长枪管和脚架 | [原创成品](../Assets/Guns/Intervention.png) |
| Barrett .50cal | [参考图](http://military.people.com.cn/mediafile/200608/28/F200608281508061813125970.jpg) / [出处](http://military.people.com.cn/GB/52935/4748546.html) | 矩形枪口制退器、长方机匣、宽直弹匣 | [原创成品](../Assets/Guns/BarrettM82.png) |
