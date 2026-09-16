<template>
  <!-- 立体船场景：船体固定、世界向后流动；数值全部由 props.live 驱动 -->
  <div ref="root" class="dredge-scene">
    <div class="shipbody">
      <div class="flowline">地质 → 耙头 → 泥泵 → 泥舱 → 溢流</div>

      <!-- 图的定位容器：HUD 与建议面板都以「图」为基准，而不是卡片内容区 -->
      <div class="figwrap">
        <!-- 图内左上角 HUD：2D / 3D 切换 + 装舱状态（顶部一条始终是天空，不会压住数据） -->
        <div class="hud-tl">
          <div class="dimtoggle" role="group" aria-label="示意图维度">
            <button class="dt" :aria-pressed="!flat" @click="flat = false">2D</button>
            <button class="dt" :aria-pressed="flat" @click="flat = true">3D</button>
          </div>
          <span id="shStateTag" class="tag brand">装舱中</span>
        </div>

        <div ref="wrapEl" class="shipwrap">
          <svg
            ref="svgEl"
            class="ship"
            :class="{ flat }"
            viewBox="0 0 1420 700"
            role="img"
            aria-label="耙吸挖泥船侧视剖面：地质层、耙头、吸泥管、泥泵、泥舱、溢流与各区域实时参数"
            @pointermove="onPointerMove"
            @pointerleave="tipPinned || hideTip()"
            @click="onSvgClick"
          >
            <defs>
              <marker id="arwG" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6.5" markerHeight="6.5" orient="auto-start-reverse">
                <path d="M0 0 L10 5 L0 10 z" fill="var(--color-text-tertiary)" />
              </marker>
              <marker id="arwS" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
                <path d="M0 0 L10 5 L0 10 z" fill="var(--v-speed)" />
              </marker>
              <marker id="arwC" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
                <path d="M0 0 L10 5 L0 10 z" fill="var(--v-cut)" />
              </marker>
              <marker id="arwJ" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
                <path d="M0 0 L10 5 L0 10 z" fill="var(--v-jflow)" />
              </marker>
              <marker id="arwP" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="3.4" markerHeight="3.4" orient="auto-start-reverse">
                <path d="M0 0 L10 5 L0 10 z" fill="var(--v-pump)" />
              </marker>
              <clipPath id="shHopperClip"><path d="M540 196 L916 196 L872 314 L584 314 Z" /></clipPath>
              <linearGradient id="shSkyG" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0" class="g-sky-top" /><stop offset="1" class="g-sky-bot" />
              </linearGradient>
              <linearGradient id="shWaterG" x1="0" y1="0" x2="0" y2=".45">
                <stop offset="0" class="g-wat-top" /><stop offset=".5" class="g-wat-mid" /><stop offset="1" class="g-wat-bot" />
              </linearGradient>
              <radialGradient id="shSunG">
                <stop offset="0" class="g-sun-core" /><stop offset=".45" class="g-sun-halo" /><stop offset="1" class="g-sun-out" />
              </radialGradient>
              <!-- 世界滚动纹理：水面波 / 水体质点纹 / 海床砂纹
                 滚动方式：整块纹理随 <g> 做 CSS transform 平移动画（连续、不是每秒跳一格），
                 周期 = 一个图案平铺宽度，动画时长由 JS 按 v 调，方向 = 向后（右） -->
              <pattern id="shWavePat" width="580" height="30" patternUnits="userSpaceOnUse">
                <path class="waveln" d="M0 14 C30 6 56 22 88 14 C120 6 146 22 178 14 C210 6 236 22 268 14 C300 6 326 22 358 14 C390 6 416 22 448 14 C480 6 506 22 538 14" />
              </pattern>
              <pattern id="shStreakPat" width="420" height="150" patternUnits="userSpaceOnUse">
                <g class="streak">
                  <line x1="30" y1="20" x2="128" y2="20" /><line x1="240" y1="52" x2="340" y2="52" />
                  <line x1="110" y1="84" x2="196" y2="84" /><line x1="300" y1="112" x2="398" y2="112" />
                  <line x1="48" y1="128" x2="128" y2="128" /><line x1="230" y1="142" x2="300" y2="142" />
                </g>
              </pattern>
              <pattern id="shBedPat" width="356" height="60" patternUnits="userSpaceOnUse">
                <g class="ripple">
                  <ellipse cx="40" cy="6" rx="13" ry="3" /><ellipse cx="126" cy="12" rx="9" ry="2.6" />
                  <ellipse cx="208" cy="5" rx="15" ry="3.2" /><ellipse cx="296" cy="11" rx="10" ry="2.8" />
                  <ellipse cx="338" cy="18" rx="7" ry="2.2" />
                </g>
              </pattern>
              <clipPath id="shBedClip"><path d="M0 440 L1215 440 L1257 414 L42 414 Z" /></clipPath>
              <!-- 开挖槽：地层带被这条裁掉，槽才读得出是"被挖走的空腔"而不是染了蓝的土 -->
              <clipPath id="shTrenchClip">
                <path d="M0 440 L1215 440 L1215 500 L1420 500 L1420 700 L0 700 Z" />
              </clipPath>
              <!-- 水体柱：天光柱只照在水里，不下到地层 -->
              <clipPath id="shDepthClip"><rect x="0" y="250" width="1420" height="192" /></clipPath>
              <!-- 舷侧裁剪：与 <path class="hull"> 用同一段 d（两处要一起改） -->
              <clipPath id="shHullClip">
                <path d="M300 186 L1112 186 L1112 322 L336 322 C296 322 266 300 256 262 C250 238 262 208 288 196 C292 194 296 189 300 186 Z" />
              </clipPath>
              <!-- 艏部弧面 + 艉封板：把船体"围起来"，否则甲板片会在艏艉两端悬空、读成一艘飞艇 -->
              <clipPath id="shHullBackClip">
                <path d="M300 186 C292 194 296 189 288 196 C262 208 250 238 256 262 C266 300 296 322 336 322 L378 296 C338 296 308 274 298 236 C292 212 304 182 330 170 C338 163 334 168 342 160 Z" />
                <path d="M1112 186 L1154 160 L1154 296 L1112 322 Z" />
              </clipPath>

              <!-- ===== 面光渐变：统一深度方向 (+42,-26)，顶面近亮远暗 / 侧面上亮下暗 ===== -->
              <linearGradient id="shTopFaceG" x1="0" y1="1" x2="0" y2="0">
                <stop offset="0" class="g-top-near" /><stop offset="1" class="g-top-far" />
              </linearGradient>
              <linearGradient id="shSideFaceG" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0" class="g-side-hi" /><stop offset="1" class="g-side-lo" />
              </linearGradient>
              <linearGradient id="shFarFaceG" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0" class="g-far-hi" /><stop offset="1" class="g-far-lo" />
              </linearGradient>
              <linearGradient id="shSandFaceG" x1="0" y1="1" x2="0" y2="0">
                <stop offset="0" class="g-sand-near" /><stop offset="1" class="g-sand-far" />
              </linearGradient>
              <linearGradient id="shWaterPlaneG" x1="0" y1="1" x2="0" y2="0">
                <stop offset="0" class="g-wp-near" /><stop offset="1" class="g-wp-far" />
              </linearGradient>
              <linearGradient id="shPitG" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0" class="g-pit-hi" /><stop offset="1" class="g-pit-lo" />
              </linearGradient>
              <linearGradient id="shAOG" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0" class="g-ao-dark" /><stop offset="1" class="g-ao-clear" />
              </linearGradient>
              <linearGradient id="shAOUpG" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0" class="g-ao-up" /><stop offset="1" class="g-ao-dark" />
              </linearGradient>
              <linearGradient id="shRayG" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0" class="g-ray-hi" /><stop offset="1" class="g-ray-out" />
              </linearGradient>
              <!-- 舷侧：干舷最亮 → 水线转水色 → 龙骨最暗（水下还有一层 shSubmergeG 加色） -->
              <linearGradient id="shHullG" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0" class="g-hull-hi" /><stop offset=".4" class="g-hull-mid" />
                <stop offset=".56" class="g-hull-lo" /><stop offset="1" class="g-hull-keel" />
              </linearGradient>
              <linearGradient id="shSubmergeG" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0" class="g-sub-clear" /><stop offset="1" class="g-sub-deep" />
              </linearGradient>
              <linearGradient id="shHouseG" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0" class="g-house-hi" /><stop offset="1" class="g-house-lo" />
              </linearGradient>
              <linearGradient id="shCloudG" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0" class="g-cloud-hi" /><stop offset="1" class="g-cloud-lo" />
              </linearGradient>
            </defs>

            <!-- 天光 · 暖阳 · 流云（云极缓慢漂移） -->
            <rect class="sky" x="0" y="0" width="1420" height="250" />
            <circle class="glow" cx="1352" cy="96" r="86" />
            <circle cx="1352" cy="96" r="26" fill="var(--color-glow-core)" opacity=".92" />
            <g id="shClouds">
              <path class="cloud" d="M1160 96 C1164 74 1186 62 1206 68 C1214 48 1244 44 1260 62 C1282 54 1306 66 1308 84 C1320 88 1326 100 1320 112 L1176 112 C1162 112 1156 106 1160 96 Z" />
              <g transform="translate(210,0)"><path class="cloud" d="M372 84 C376 62 398 52 416 58 C424 40 452 38 466 54 C484 48 504 58 506 76 C516 80 520 92 514 100 L386 100 C374 100 368 94 372 84 Z" opacity=".7" /></g>
            </g>

            <!-- 水体（世界向后 = 向右流动） -->
            <rect class="water" x="0" y="250" width="1420" height="450" />
            <!-- 立体：水面片（向后退 = 右上）+ 远处水天交界的亮线 -->
            <g class="depth">
              <path class="d-water" d="M0 250 L1420 250 L1420 224 L42 224 Z" />
              <line class="horizon" x1="42" y1="224" x2="1420" y2="224" />
            </g>
            <!-- 水面波：整块纹理右移（周期 = 图案宽度 580，超出画布的部分用来无缝衔接） -->
            <g id="shWaves" class="scrollg"><rect class="wavesurf" x="-580" y="250" width="2580" height="30" /></g>
            <!-- 水体质点纹：同法滚动 -->
            <g id="shStreaks" class="scrollg"><rect class="streaks" x="-420" y="300" width="2260" height="140" /></g>
            <!-- 水下天光柱：太阳在右上 → 光柱向左下斜插，越深越淡（水体体积感主要来自这里） -->
            <g id="shRays" clip-path="url(#shDepthClip)" opacity=".55">
              <path class="ray" d="M1150 250 L1252 250 L1086 442 L942 442 Z" />
              <path class="ray" d="M1302 250 L1364 250 L1246 442 L1152 442 Z" />
              <path class="ray" d="M884 250 L932 250 L748 442 L672 442 Z" />
            </g>
            <!-- 地层（当前开挖层高亮）：整层被子口裁掉开挖槽那一段，槽内才是水不是土 -->
            <g clip-path="url(#shTrenchClip)">
              <g id="shBands">
                <rect id="shB1" class="band" x="0" y="440" width="1420" height="72" fill="var(--soil-1)" />
                <rect id="shB2" class="band" x="0" y="512" width="1420" height="68" fill="var(--soil-2)" />
                <rect id="shB3" class="band" x="0" y="580" width="1420" height="120" fill="var(--soil-3)" />
              </g>
              <!-- 层理线：沉积层的分界，压一点暗线让地层有厚度（不参与开挖高亮） -->
              <g class="bedline">
                <line x1="0" y1="512" x2="1215" y2="512" />
                <line x1="0" y1="580" x2="1215" y2="580" />
              </g>
            </g>
            <!-- 未挖海床顶面（滚动的砂纹）→ 开挖面竖壁 → 已挖槽（耙头在两者交界处切削） -->
            <g clip-path="url(#shBedClip)">
              <path class="d-sand" d="M0 440 L1215 440 L1257 414 L42 414 Z" />
              <g id="shBedScroll" class="scrollg"><rect class="bedtex" x="-356" y="400" width="2132" height="60" /></g>
            </g>
            <path class="d-side" d="M1215 440 L1215 500 L1257 474 L1257 414 Z" />
            <!-- 槽内水：越深越浓 → 槽底一条落影，槽才读得出深度 -->
            <path class="d-pit" d="M1225 440 L1420 440 L1420 500 L1225 500 Z" />
            <rect class="pit-ao" x="1225" y="470" width="195" height="30" />
            <path class="d-side" d="M1215 440 L1225 440 L1225 500 L1215 500 Z" />
            <path class="d-water" d="M1225 500 L1420 500 L1462 474 L1267 474 Z" />
            <!-- 原泥面（艏侧未挖） -->
            <g class="dashline" stroke="var(--color-text-tertiary)" stroke-width="1" stroke-dasharray="5 5" opacity=".8">
              <line x1="20" y1="440" x2="1180" y2="440" />
            </g>
            <text class="lbl" x="470" y="432">原泥面</text>

            <!-- 切削深度尺寸（贴在耙头前方的未挖土体上） -->
            <g id="shTdim">
              <line x1="1152" y1="440" x2="1168" y2="440" stroke="var(--v-cut)" stroke-width="1.2" />
              <line x1="1152" y1="500" x2="1168" y2="500" stroke="var(--v-cut)" stroke-width="1.2" />
              <line
                x1="1160" y1="443" x2="1160" y2="497" stroke="var(--v-cut)" stroke-width="1.4"
                marker-start="url(#arwC)" marker-end="url(#arwC)"
              />
              <rect class="chip" x="1054" y="504" width="146" height="28" rx="5" />
              <text id="shF_tdim" class="val" x="1064" y="524" fill="var(--v-cut)">t —</text>
            </g>

            <!-- 缆绳（锚点在艏侧海床上，船端固定在绞车） -->
            <g stroke="var(--color-text-tertiary)" stroke-width="1.1">
              <line id="shCable1" x1="292" y1="176" x2="48" y2="428" />
              <line id="shCable2" x1="292" y1="182" x2="44" y2="442" />
              <circle cx="48" cy="428" r="3" fill="var(--color-text-tertiary)" stroke="none" />
              <circle cx="44" cy="442" r="3" fill="var(--color-text-tertiary)" stroke="none" />
            </g>

            <!-- 水线（世界坐标） -->
            <line x1="0" y1="250" x2="1420" y2="250" stroke="var(--color-info)" stroke-width="1.2" stroke-dasharray="7 6" opacity=".75" />

            <!-- 耙头扰动羽流：泥沙在水里被冲散、随水流向右飘（不随船横摇；透明度由射流压力驱动） -->
            <g id="shPlume" class="plume">
              <ellipse cx="1300" cy="476" rx="42" ry="18" />
              <ellipse cx="1352" cy="462" rx="32" ry="14" />
              <ellipse cx="1386" cy="480" rx="20" ry="10" />
            </g>

            <!-- ============ 船：整体随横移摆动 + 轻微横摇（数值标签不带入摆动） ============ -->
            <g id="shMoving">
              <!-- 立体：水中的船影 → 甲板顶面 / 上层建筑体块 / 烟囱 -->
              <g class="depth">
                <path class="d-sub" d="M300 186 L1112 186 L1112 322 L336 322 C296 322 266 300 256 262 C250 238 262 208 288 196 C292 194 296 189 300 186 Z" transform="translate(0,7)" />
                <path class="d-top" d="M300 186 L1112 186 L1154 160 L342 160 Z" />
                <path class="d-top" d="M330 106 L452 106 L494 80 L372 80 Z" />
                <path class="d-side" d="M452 106 L494 80 L494 160 L452 186 Z" />
                <rect class="d-side" x="486" y="126" width="6" height="60" />
                <ellipse class="d-top" cx="479" cy="126" rx="13" ry="5" />
                <!-- 艏部弧面（前后轮廓之间的那条带）+ 艉封板：船体两端不再是悬空的片 -->
                <path class="d-side" d="M300 186 C292 194 296 189 288 196 C262 208 250 238 256 262 C266 300 296 322 336 322 L378 296 C338 296 308 274 298 236 C292 212 304 182 330 170 C338 163 334 168 342 160 Z" />
                <path class="d-side" d="M1112 186 L1154 160 L1154 296 L1112 322 Z" />
              </g>
              <!-- 舷侧：一条竖直渐变打底，再叠三条横向带 —— 舷侧亮带（贴甲板）/ 水线漆 / 水下加色。
                 水下那层是"船确实泡在水里"的主要线索，2D 视图也保留（不是立体投影层）。 -->
              <path class="hull" d="M300 186 L1112 186 L1112 322 L336 322 C296 322 266 300 256 262 C250 238 262 208 288 196 C292 194 296 189 300 186 Z" />
              <g clip-path="url(#shHullClip)">
                <rect class="hull-strake" x="240" y="186" width="900" height="14" />
                <rect class="hull-boot" x="240" y="243" width="900" height="12" />
                <rect class="hull-sub" x="240" y="250" width="900" height="80" />
              </g>
              <!-- 艏弧面/艉封板的水下部分也要吃水色，否则船头会有一段"干的"弧面漂在水里 -->
              <g clip-path="url(#shHullBackClip)">
                <rect class="hull-sub" x="240" y="250" width="940" height="80" />
              </g>
              <!-- 水线泡沫：水在动、船不动，泡沫就贴着舷侧；被泥舱剖面盖掉中间一段，读作剖切 -->
              <g class="foam">
                <path class="foam-soft" d="M258 250 C330 246 400 254 470 250 C540 246 610 254 680 250 C750 246 820 254 890 250 C960 246 1030 254 1100 250 C1114 249 1120 249 1124 249" />
                <path class="foam-core" d="M258 250 C330 246 400 254 470 250 C540 246 610 254 680 250 C750 246 820 254 890 250 C960 246 1030 254 1100 250 C1114 249 1120 249 1124 249" />
              </g>
              <!-- 船尾下游的散碎泡沫：船不动但水在往后走，船尾总得有点拖尾 -->
              <g class="foam">
                <path class="foam-soft" d="M1128 251 C1176 248 1230 254 1288 250 C1318 248 1340 251 1356 250" />
              </g>
              <!-- 上层建筑 -->
              <rect class="house" x="330" y="106" width="122" height="80" rx="3" />
              <g fill="var(--color-bg-elevated)"><rect x="340" y="118" width="102" height="12" rx="2" /><rect x="340" y="138" width="102" height="12" rx="2" /></g>
              <text class="lbl" x="391" y="176" text-anchor="middle">驾驶室</text>
              <rect class="house" x="466" y="126" width="26" height="60" rx="2" />
              <!-- 前甲板横移绞车（底边压到舷侧，别在艏部弧线上方悬空） -->
              <rect class="house" x="286" y="170" width="28" height="26" rx="2" />
              <!-- 艏侧推 -->
              <circle cx="278" cy="264" r="10" class="inner" />
              <path d="M272 258 L284 258 M272 270 L284 270" stroke="var(--color-text-secondary)" stroke-width="1.4" />
              <!-- 泥舱（舱体 → 舱内远壁与舱底 → 舱内泥面 → 舱口轮廓） -->
              <path class="shell" d="M540 196 L916 196 L872 314 L584 314 Z" />
              <g class="depth">
                <path class="d-far" d="M582 170 L958 170 L914 288 L626 288 Z" />
                <path class="d-side" d="M584 314 L872 314 L914 288 L626 288 Z" />
              </g>
              <rect id="shFill" x="520" y="314" width="420" height="0" clip-path="url(#shHopperClip)" fill="var(--soil-1)" />
              <polygon id="shSurface" class="surface depth" points="584,314 872,314 914,288 626,288" fill="var(--soil-1)" opacity="0" />
              <!-- 舱内上沿落影 + 舱底压暗：舱口一圈压暗、越往舱底越沉，泥舱才读得出是个"深舱" -->
              <rect class="hopper-ao" x="536" y="196" width="384" height="24" clip-path="url(#shHopperClip)" />
              <rect class="hopper-deep" x="536" y="240" width="384" height="74" clip-path="url(#shHopperClip)" />
              <path class="inner" d="M540 196 L916 196 L872 314 L584 314 Z" />
              <text id="shF_hopPct" class="valbig" x="700" y="228" text-anchor="middle">—</text>
              <text id="shF_hopInner" class="lbl" x="700" y="250" text-anchor="middle">—</text>
              <!-- 舱内溢流堰 -->
              <g class="inner" stroke-dasharray="4 4">
                <line x1="640" y1="196" x2="640" y2="300" /><line x1="816" y1="196" x2="816" y2="300" />
              </g>
              <!-- 泵舱隔断 + 泥泵（圆内 = 输土占用） -->
              <line class="inner" stroke-dasharray="5 5" x1="956" y1="186" x2="956" y2="322" />
              <circle id="shPump" cx="1094" cy="296" r="17" class="shell" />
              <path d="M1094 281 L1094 290 M1094 302 L1094 311" stroke="var(--color-text-secondary)" stroke-width="1.4" />
              <text id="shF_pct" class="lbl" x="1094" y="300" text-anchor="middle">—</text>
              <text class="lbl" x="1094" y="270" text-anchor="middle">泥泵</text>
              <text id="shF_rpm" class="lbl" x="1074" y="338" text-anchor="middle">—</text>

              <!-- 溢流：堰 → 上甲板 → 舷外 -->
              <g>
                <path class="flow ovf" d="M608 200 L608 158" marker-end="url(#arwP)" />
                <path class="flow ovf" d="M760 200 L760 158" marker-end="url(#arwP)" />
                <text class="lbl" x="684" y="164" text-anchor="middle">溢流</text>
              </g>

              <!-- 管路：耙头 → 泥泵 → 泥舱（先画管身暗侧，再画亮面 + 泥浆流） -->
              <g>
                <path class="tube-far depth" d="M1246 486 C1210 462 1176 420 1152 380 C1132 346 1120 312 1110 300 L1111 296" transform="translate(9,-6)" />
                <path class="tube-far depth" d="M1082 286 C1050 256 1000 224 956 210 L902 200" transform="translate(9,-6)" />
                <path class="pipe" d="M1246 486 C1210 462 1176 420 1152 380 C1132 346 1120 312 1110 300 L1111 296" />
                <path class="pipein" d="M1246 486 C1210 462 1176 420 1152 380 C1132 346 1120 312 1110 300 L1111 296" />
                <path id="shFlowIn" class="flow" d="M1246 486 C1210 462 1176 420 1152 380 C1132 346 1120 312 1110 300 L1111 296" />
                <path class="pipe" d="M1082 286 C1050 256 1000 224 956 210 L902 200" />
                <path class="pipein" d="M1082 286 C1050 256 1000 224 956 210 L902 200" />
                <path id="shFlowOut" class="flow" d="M1082 286 C1050 256 1000 224 956 210 L902 200" marker-end="url(#arwP)" />
                <text class="lbl" x="1164" y="352" transform="rotate(-52 1164 352)">吸泥管</text>
              </g>

              <!-- 耙头 + 冲水射流 -->
              <g id="shHead">
                <path class="d-top depth" d="M1206 470 L1252 465 L1294 439 L1248 444 Z" />
                <path class="shell" d="M1206 470 L1252 465 L1263 484 L1214 497 Z" />
                <g stroke="var(--color-text-secondary)" stroke-width="1.6">
                  <line x1="1222" y1="494" x2="1220" y2="503" /><line x1="1234" y1="491" x2="1232" y2="501" />
                  <line x1="1246" y1="488" x2="1244" y2="499" /><line x1="1257" y1="485" x2="1256" y2="497" />
                </g>
                <g id="shJets" class="jets">
                  <path d="M1254 472 C1266 464 1274 456 1280 448" />
                  <path d="M1258 479 C1270 474 1280 468 1288 460" />
                  <path d="M1261 486 C1273 484 1284 480 1292 474" />
                </g>
                <text class="lbl" x="1216" y="462">耙头</text>
              </g>

              <!-- 图上建议卡：放在船艏前方。第一行 = 建议产量（大字）+ 增益；下面每个可调杆各占一行
               并带中文名（避免 v / pj 这类缩写看不懂），最后一行是「下发指令」按钮 -->
              <g id="shAdvCard">
                <rect id="shAdvChip" class="chip" x="8" y="250" width="236" height="112" rx="10" stroke="var(--v-speed)" />
                <text class="lbl" x="20" y="270">前方建议 · T+5 s</text>
                <text id="shAdvResult" class="valbig" x="20" y="298" fill="var(--v-hex)">—</text>
                <text id="shAdvGain" class="val" x="150" y="298" fill="var(--v-speed)">—</text>
                <text id="shAdvLine1" class="adv" x="20" y="318">—</text>
                <text id="shAdvLine2" class="adv" x="20" y="335" />
                <text id="shAdvLine3" class="adv" x="20" y="352" />
                <text id="shAdvLine4" class="adv" x="20" y="369" />
                <g
                  id="shAdvBtn"
                  class="svgbtn"
                  role="button"
                  tabindex="0"
                  aria-label="下发指令"
                  @click="dispatchAdvice"
                  @keydown="onAdvBtnKey"
                >
                  <rect id="shAdvBtnBg" x="20" y="378" width="212" height="28" rx="8" fill="var(--color-brand)" />
                  <text id="shAdvBtnTxt" class="btntxt" x="126" y="397" text-anchor="middle">下发指令</text>
                </g>
                <path id="shAdvLead" class="inner" stroke-dasharray="3 4" d="M244 390 L288 312" />
              </g>
              <!-- 建议值（ghost）：更深的切深、更长的横移 -->
              <g id="shHints">
                <line id="shTghost" class="ghost" x1="1150" y1="500" x2="1256" y2="500" stroke="var(--v-cut)" stroke-width="1.6" opacity="0" />
                <line
                  id="shVA2" class="ghost" x1="1198" y1="496" x2="1000" y2="496" stroke="var(--v-speed)" stroke-width="1.6"
                  marker-end="url(#arwS)" opacity="0"
                />
              </g>

              <!-- 横移方向与速度（随船摆动，箭头方向 = 当前摆动方向） -->
              <g>
                <line id="shVA" x1="1198" y1="486" x2="1000" y2="486" stroke="var(--v-speed)" stroke-width="2.4" marker-end="url(#arwS)" />
                <text id="shVtxt" class="val" x="1190" y="478" text-anchor="end" fill="var(--v-speed)">—</text>
              </g>
            </g><!-- /shMoving -->

            <!-- ============ 固定标注层（不随船移动；左上留产量，右上留给建议面板） ============ -->
            <g>
              <!-- 产量卡原来是 20→108，会盖掉驾驶室的顶面；整体上移、卡高压到 72，正好让出船楼 -->
              <rect class="chip" x="300" y="4" width="200" height="72" rx="14" stroke="var(--v-hex)" stroke-width="1.8" />
              <text class="lbl" x="316" y="28">h_ex 产量 m³/h</text>
              <text id="shF_hex" class="valbig" x="316" y="64">—</text>
              <line id="shHexLead" x1="400" y1="78" x2="556" y2="192" stroke="var(--v-hex)" stroke-width="2" marker-end="url(#arwC)" />
            </g>
            <!-- 地层读数：贴在耙头前方的未挖土体上（正在被切的那一层） -->
            <g>
              <rect class="chip" x="992" y="400" width="150" height="52" rx="8" />
              <text id="shF_soil" class="cap" x="1004" y="420">—</text>
              <text id="shF_ucs" class="val" x="1004" y="440" fill="var(--v-ucs)">—</text>
            </g>

            <!-- hover / click 命中区（最上层，透明） -->
            <g id="shHits">
              <rect class="hit" data-z="soil" x="4" y="430" width="1412" height="86" />
              <rect class="hit" data-z="soil" x="4" y="516" width="1412" height="64" />
              <rect class="hit" data-z="soil" x="4" y="580" width="1412" height="116" />
              <rect class="hit" data-z="head" x="1150" y="400" width="200" height="122" />
              <rect class="hit" data-z="hopper" x="490" y="162" width="490" height="166" />
              <rect class="hit" data-z="pump" x="1000" y="248" width="150" height="96" />
              <rect class="hit" data-z="hull" x="236" y="76" width="300" height="250" />
              <rect class="hit" data-z="prod" x="294" y="14" width="212" height="100" />
            </g>
          </svg>
          <div v-show="tipOpen" ref="tipEl" class="tip" :style="{ '--tip-c': tipColor }">
            <div class="tip-h">
              <b>{{ tipTitle }}</b>
              <button class="tip-x" aria-label="关闭" @click="hideTip">✕</button>
            </div>
            <div id="tipBody">
              <div v-for="([k, v], i) in tipRows" :key="i" class="r"><em>{{ k }}</em><b>{{ v }}</b></div>
            </div>
          </div>
        </div><!-- /shipwrap -->

        <!-- 建议面板：宽屏浮在图右上角；窄屏（静态）紧跟在图下方 -->
        <div class="panel">
          <div class="panel-h">
            <b>耙头操作建议</b>
            <span id="shAdvTag" class="tag ok">可行</span>
          </div>
          <div id="shipAdvice" />
        </div>
      </div><!-- /figwrap -->

      <div class="ship-legend">
        <span><i style="background: var(--v-pump)" />泥浆流（越快 = Q<sub>m</sub> 越大）</span>
        <span><i style="background: var(--color-info)" />溢流回流</span>
        <span><i style="background: var(--v-jetp)" />冲水射流</span>
        <span><i class="dash" style="color: var(--v-cut)" />虚线 = 建议值</span>
        <span>悬停 / 点按船上任一处看细节</span>
        <span>船体固定 · 场景按 v 向后流动</span>
      </div>
      <div id="shipMobile" class="mgrid" />
    </div>
    <details class="assume">
      <summary>区域 → 参数 → 流向 <span class="hint" style="margin-left:auto">点开看 5 行表</span></summary>
      <div class="abody">
        <div class="tblwrap">
          <table class="tbl">
            <thead><tr><th>区域</th><th>关键参数</th><th>流向 / 依赖</th></tr></thead>
            <tbody>
              <tr>
                <td>地质 · 前方土体</td>
                <td>UCS · 层厚 · 含水率</td>
                <td>→ 决定 η<sub>c</sub>、k<sub>j</sub></td>
              </tr>
              <tr>
                <td>耙头 · 唯一可调</td>
                <td>t · v · p<sub>j</sub> · Q<sub>j</sub></td>
                <td>→ h<sub>c</sub> = W·t·v·η<sub>c</sub>，h<sub>j</sub> = Q<sub>j</sub>·k<sub>j</sub></td>
              </tr>
              <tr>
                <td>吸泥管 + 泥泵</td>
                <td>rpm · Q<sub>m</sub></td>
                <td>→ 输移上限 Q<sub>m</sub>·cv<sub>max</sub>，超了进不了舱</td>
              </tr>
              <tr>
                <td>泥舱</td>
                <td>a·h<sub>c</sub> + b·h<sub>j</sub> · 舱载</td>
                <td>→ 固相留舱，水走溢流；满舱转抛泥</td>
              </tr>
              <tr>
                <td>计量 + 标定</td>
                <td>实测 h<sub>ex</sub></td>
                <td>→ 解 a、b，闭环回耙头</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div class="k-note" style="margin-top:10px">耙头是<b>唯一可现场调节</b>的环节；泥泵与浓度只决定「能不能送进舱」，不产生土。</div>
      </div>
    </details>
  </div>
</template>

<script setup lang="ts">
/* ==========================================================================
   疏浚机理 · 立体船场景（视图 A）
   —— 从原型 docs/prototypes/dredge-mechanism.html 移植。

   为什么这里是「声明式模板 + 一个命令式更新过程」而不是把几十个属性全写成绑定：
   SVG 有 ~50 个随工况变化的几何属性（舱内泥面高度、液面四角、箭头长度、建议卡高度…），
   原型里这套更新逻辑已经逐项验证过；逐属性改写成绑定既冗长又容易改错。
   所以本组件像「画布」：Vue 管结构与生命周期，paint() 按 live 写属性。
   ========================================================================== */
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import type { DredgeLive, DredgeZone } from '@/types'
import {
  DREDGE_K,
  LEVERS,
  SOILS,
  ZONE_COLOR,
  ZONE_TITLE,
  buildTipRows,
  formatLever,
  leverChanged,
  nf,
  soilColorVar,
  soilMark,
} from '@shared/core/dredge'

const props = defineProps<{
  /** 当前工况快照（每个 tick 由页面刷新一次） */
  live: DredgeLive
  /** 是否是当前可见视图（不可见时不跑滚动动画） */
  active: boolean
}>()

const emit = defineEmits<{
  /** 下发指令：由页面写入事件流并刷新 */
  dispatch: []
}>()

const root = ref<HTMLElement | null>(null)
const svgEl = ref<SVGSVGElement | null>(null)
const wrapEl = ref<HTMLElement | null>(null)
const tipEl = ref<HTMLElement | null>(null)

/** 三维视图开关（关掉 = 隐藏斜投影层，几何位置与数值不变） */
const flat = ref(false)
/** 详情卡：钉住（点按）或跟手（悬停） */
const tipZone = ref<DredgeZone | null>(null)
const tipPinned = ref(false)
const tipTitle = ref('—')
const tipRows = ref<Array<[string, string]>>([])
const tipOpen = computed(() => tipZone.value !== null)
const tipColor = computed(() => (tipZone.value ? ZONE_COLOR[tipZone.value] : 'var(--color-brand)'))
/** 横摇角 deg（由 tick 推出，纯函数） */
const roll = ref(0)

/** 模块内 [id] 元素缓存：一帧要写几十个属性，逐次 querySelector 太碎 */
let el: Record<string, Element> = {}
function cacheEls(): void {
  el = {}
  root.value?.querySelectorAll('[id]').forEach((node) => {
    el[node.id] = node
  })
}
function set(id: string, attr: string, value: string | number): void {
  el[id]?.setAttribute(attr, String(value))
}
function txt(id: string, value: string): void {
  const node = el[id]
  if (node) node.textContent = value
}
function styleOf(id: string): CSSStyleDeclaration | undefined {
  return (el[id] as (Element & { style?: CSSStyleDeclaration }) | undefined)?.style
}

/* ---------- 一个 tick：把 live 写进场景 ---------- */
function paint(): void {
  if (!el.shMoving) return
  const L = props.live
  const s = L.setting
  const rec = L.production
  const best = L.best
  const cp = L.bottomPressure
  const Qm = L.slurryFlow
  const pctCap = L.capacityUsed
  const frac = L.hopperFrac
  const discharging = L.discharging

  /* 地层：当前开挖层高亮，其余压暗 */
  for (let i = 1; i <= 3; i++) el[`shB${i}`]?.classList.toggle('dim', i - 1 !== s.soil)
  txt('shF_soil', `${soilMark(s.soil)} ${SOILS[s.soil].name}`)
  txt('shF_ucs', `UCS ${nf(s.ucs)} kPa`)
  txt('shStateTag', discharging ? '舱满 · 抛泥中' : '装舱中')
  el.shStateTag?.setAttribute('class', `tag ${discharging ? 'warn' : 'brand'}`)

  /* 泥舱：装舱进度（舱内大字 % + m³） */
  const fh = 118 * frac
  set('shFill', 'y', (314 - fh).toFixed(1))
  set('shFill', 'height', fh.toFixed(1))
  set('shFill', 'fill', soilColorVar(s.soil))
  /* 立体舱内泥面：按装载率插值近/远两条液面边（深度 +42,-26），与近壁液位同步 */
  const sy = 314 - 118 * frac
  const nL = 584 - 44 * frac
  const nR = 872 + 44 * frac
  set('shSurface', 'points', `${nL.toFixed(1)},${sy.toFixed(1)} ${nR.toFixed(1)},${sy.toFixed(1)} `
  + `${(nR + 42).toFixed(1)},${(sy - 26).toFixed(1)} ${(nL + 42).toFixed(1)},${(sy - 26).toFixed(1)}`)
  set('shSurface', 'fill', soilColorVar(s.soil))
  set('shSurface', 'opacity', frac > 0.02 ? '1' : '0')
  txt('shF_hopPct', `${(frac * 100).toFixed(0)}%`)
  set('shF_hopPct', 'fill', discharging ? 'var(--color-warning)' : 'var(--color-text-primary)')
  txt('shF_hopInner', `${nf(L.hopper)} m³`)

  /* 流动速度可视化：虚线行进周期随泥浆流量变化 */
  const dur = `${Math.max(0.45, Math.min(3, 2600 / Qm)).toFixed(2)}s`
  const flowIn = styleOf('shFlowIn')
  const flowOut = styleOf('shFlowOut')
  const jets = styleOf('shJets')
  const plume = styleOf('shPlume')
  if (flowIn) flowIn.animationDuration = dur
  if (flowOut) flowOut.animationDuration = dur
  if (jets) jets.opacity = (0.28 + 0.72 * (s.pj / DREDGE_K.pjMax)).toFixed(2)
  /* 耙头扰动羽流：冲水压力越大、搅起来的泥沙越多 */
  if (plume) plume.opacity = (0.35 + 0.5 * Math.min(1, s.pj / DREDGE_K.pjMax)).toFixed(2)

  /* 泵舱：圆内 = 输土占用，圆下 = 转速 */
  txt('shF_pct', `${pctCap.toFixed(0)}%`)
  const capTone = pctCap >= 94
    ? 'var(--color-danger)'
    : pctCap >= 84 ? 'var(--color-warning)' : 'var(--color-text-tertiary)'
  set('shF_pct', 'fill', capTone)
  set('shPump', 'stroke', capTone)
  txt('shF_rpm', `${nf(s.rpm)} rpm`)
  txt('shF_hex', `${nf(rec.hexMech)} m³/h`)

  /* 船体固定显示（只保留轻微横摇）；相对位移由世界滚动表达 */
  set('shMoving', 'transform', `rotate(${roll.value.toFixed(2)} 700 254)`)
  set('shClouds', 'transform', `translate(${(Math.sin(L.tick * 0.06) * 30).toFixed(1)},0)`)

  /* 横移箭头：箭杆长度 ∝ v */
  const vLen = (s.v / DREDGE_K.vMax) * 260
  set('shVA', 'x1', 1198)
  set('shVA', 'x2', (1198 - vLen).toFixed(0))
  txt('shVtxt', `${s.v.toFixed(2)} kn`)
  txt('shF_tdim', `t ${s.t.toFixed(2)} m`)

  /* 建议值 ghost：更深的槽底 / 更长的横移 */
  if (best) {
    const dt = best.s.t - s.t
    const off = leverChanged('t', s.t, best.s.t)
      ? Math.sign(dt) * Math.min(26, (60 * Math.abs(dt)) / Math.max(s.t, 0.05))
      : 0
    set('shTghost', 'y1', (500 + off).toFixed(1))
    set('shTghost', 'y2', (500 + off).toFixed(1))
    set('shTghost', 'opacity', off === 0 ? '0' : '.85')
    set('shVA2', 'x2', (1198 - (best.s.v / DREDGE_K.vMax) * 260).toFixed(0))
    set('shVA2', 'opacity', leverChanged('v', s.v, best.s.v) ? '.85' : '0')
  } else {
    set('shTghost', 'opacity', '0')
    set('shVA2', 'opacity', '0')
  }

  /* 图上建议卡（船艏前方）：第 1 行 = 建议产量 + 增益，下面每根可调杆各占一行带中文名，
     这样 v / pj 这类缩写不会看不懂。面板只留约束条，不重复结论。 */
  const LEV_TOP = 318
  const LEV_LH = 17
  const CARD_TOP = 250
  const CARD_X = 20
  const BTN_H = 28
  const LEVER_CN: Record<string, string> = {
    t: '切削深度 t',
    v: '横移速度 v',
    pj: '冲水压力 pj',
    rpm: '泥泵转速 rpm',
  }
  const tw = (str: string, em = 7.8): number =>
    [...str].reduce((n, ch) => n + (/[\u4e00-\u9fff（）]/.test(ch) ? 14 : em), 0)
  const rows = best
    ? LEVERS.filter((LE) => leverChanged(LE.k, s[LE.k], best.s[LE.k]))
        .map((LE) => `${LEVER_CN[LE.k]} ${formatLever(LE, s[LE.k])}→${formatLever(LE, best.s[LE.k])} ${LE.u}`)
    : []
  if (!rows.length) rows.push(best ? '维持现状（当前已是最优）' : '已顶到约束边界')
  const btnY = LEV_TOP + rows.length * LEV_LH + 5
  const chipH = btnY + BTN_H - CARD_TOP + 8
  for (let i = 0; i < 4; i++) {
    const node = el[`shAdvLine${i + 1}`]
    if (!node) continue
    node.textContent = rows[i] ?? ''
    node.setAttribute('y', String(LEV_TOP + i * LEV_LH))
  }
  set('shAdvBtnBg', 'y', btnY)
  set('shAdvBtnTxt', 'y', btnY + 19)
  el.shAdvBtn?.classList.toggle('off', !best)
  set('shAdvBtn', 'aria-disabled', String(!best))
  const bigTxt = best ? `${nf(best.hex)} m³/h` : '受限'
  set('shAdvChip', 'height', chipH)
  txt('shAdvResult', bigTxt)
  set('shAdvGain', 'x', (CARD_X + tw(bigTxt, 14.4) + 12).toFixed(0))
  txt('shAdvGain', best ? `${best.gainPct >= 0 ? '+' : ''}${best.gainPct.toFixed(1)} %` : '')
  set('shAdvLead', 'd', `M244 ${CARD_TOP + chipH - 10} L288 312`)

  /* 面板：只留约束条（结论已移到图上） */
  const tag = el.shAdvTag
  if (tag) {
    tag.textContent = best ? '可行' : '受限'
    tag.setAttribute('class', `tag ${best ? 'ok' : 'warn'}`)
  }
  const cons: Array<[string, number, number, (v: number) => string, string, string]> = [
    ['触底', cp, DREDGE_K.cpMax, (v) => nf(v), 'kPa', 'var(--v-cut)'],
    ['浓度', rec.cv * 100, DREDGE_K.cvMax * 100, (v) => v.toFixed(1), '%', 'var(--v-pump)'],
    ['泵占用', pctCap, 100, (v) => v.toFixed(0), '%', 'var(--v-hex)'],
    ['横移', s.v, DREDGE_K.vMax, (v) => v.toFixed(2), 'kn', 'var(--v-speed)'],
  ]
  const advice = el.shipAdvice
  if (advice) {
    advice.innerHTML = cons.map((c) => {
      const p = (100 * c[1]) / c[2]
      const tone = p >= 94 ? 'danger' : p >= 84 ? 'warn' : ''
      const bg = tone ? '' : `background:${c[5]}`
      return `<div class="crow"><span>${c[0]}</span>`
        + `<div class="bar"><i class="${tone}" style="width:${Math.min(100, p).toFixed(0)}%;${bg}"></i></div>`
        + `<span class="cv"><b>${c[3](c[1])}</b>/${c[3](c[2])} ${c[4]}</span></div>`
    }).join('')
  }

  /* 手机端数值卡：场景需横向滑动，读数改在下方卡片里 */
  const mobile = el.shipMobile
  if (mobile) {
    const cells: Array<[string, string]> = [
      ['地层', `${soilMark(s.soil)} ${SOILS[s.soil].name}`],
      ['UCS / 层厚', `${nf(s.ucs)} kPa · ${s.layer.toFixed(1)} m`],
      ['合成 h_ex', `${nf(rec.hexMech)} m³/h`],
      ['舱载', `${nf(L.hopper)} / ${nf(DREDGE_K.hopperCap)} m³`],
      ['浓度 Cv', `${(rec.cv * 100).toFixed(1)} / ${(DREDGE_K.cvMax * 100).toFixed(0)} %`],
      ['泥泵', `${nf(s.rpm)} rpm · 占用 ${pctCap.toFixed(0)} %`],
      ['建议产量', best ? `${nf(best.hex)} m³/h（${best.gainPct >= 0 ? '+' : ''}${best.gainPct.toFixed(1)} %）` : '维持现状'],
      ['横移速度 v', `${s.v.toFixed(2)} / ${DREDGE_K.vMax} kn`],
    ]
    mobile.innerHTML = cells
      .map(([k, v]) => `<div class="mcell"><span class="k">${k}</span><b>${v}</b></div>`)
      .join('')
  }

  /* 详情卡：钉住或悬浮时按最新工况刷新内容（不重新定位，避免鼠标下抖动） */
  if (tipZone.value) tipRows.value = buildTipRows(tipZone.value, L)
}

/* ---------- 世界滚动（rAF 逐帧，连续、无相位跳变） ---------- */
const SCROLL = { waves: 24, streaks: 30, bed: 44 } // 各层速度 px/s（@ v = vMax）
const TILE = { waves: 580, streaks: 420, bed: 356 } // 各层图案周期 px
const reduceMotion = typeof window !== 'undefined'
  && window.matchMedia('(prefers-reduced-motion: reduce)').matches
let scroll = 0
let lastTs = 0
let raf = 0
function scrollLoop(ts: number): void {
  if (!lastTs) lastTs = ts
  const dt = Math.min(0.1, (ts - lastTs) / 1000)
  lastTs = ts
  /* 视图不可见时不推进，省电 */
  if (props.active && !reduceMotion) {
    scroll += dt * Math.max(0.25, props.live.setting.v / DREDGE_K.vMax)
    const off = (k: keyof typeof SCROLL): string => ((scroll * SCROLL[k]) % TILE[k]).toFixed(1)
    set('shWaves', 'transform', `translate(${off('waves')},0)`)
    set('shStreaks', 'transform', `translate(${off('streaks')},0)`)
    set('shBedScroll', 'transform', `translate(${off('bed')},0)`)
  }
  raf = requestAnimationFrame(scrollLoop)
}

/* ---------- 详情卡定位（桌面跟手浮卡 / 手机底部抽屉） ---------- */
function showTip(zone: DredgeZone, clientX: number, clientY: number): void {
  tipZone.value = zone
  if (tipPinned.value) return
  const tip = tipEl.value
  const wrap = wrapEl.value
  if (!tip || !wrap) return
  /* 手机端走底部抽屉：位置交给 CSS，必须清掉内联 left/top，
     否则内联样式会盖掉 fixed 的 left/right/bottom，把抽屉压成一根窄长条 */
  if (window.matchMedia('(max-width: 860px)').matches) {
    tip.style.left = ''
    tip.style.top = ''
    return
  }
  const r = wrap.getBoundingClientRect()
  const sc = wrap.scrollLeft || 0
  const w = tip.offsetWidth || 360
  const h = tip.offsetHeight || 150
  const x = Math.max(sc + 6, Math.min(clientX - r.left + sc + 16, sc + r.width - w - 10))
  const y = Math.max(6, Math.min(clientY - r.top + 14, Math.max(6, r.height - h - 10)))
  tip.style.left = `${x.toFixed(0)}px`
  tip.style.top = `${y.toFixed(0)}px`
}
function hideTip(): void {
  tipPinned.value = false
  tipZone.value = null
}
function onPointerMove(e: PointerEvent): void {
  if (e.pointerType === 'touch') return // 触屏只认点按，滑动时不弹卡
  const hit = (e.target as Element | null)?.closest?.('[data-z]')
  if (!hit) {
    if (!tipPinned.value) hideTip()
    return
  }
  showTip(hit.getAttribute('data-z') as DredgeZone, e.clientX, e.clientY)
}
function onSvgClick(e: MouseEvent): void {
  const hit = (e.target as Element | null)?.closest?.('[data-z]')
  if (!hit) {
    hideTip()
    return
  }
  const zone = hit.getAttribute('data-z') as DredgeZone
  if (tipPinned.value && tipZone.value === zone) {
    hideTip()
    return
  }
  hideTip()
  tipPinned.value = true
  showTip(zone, e.clientX, e.clientY)
}

/* ---------- 下发指令（图上 SVG 按钮） ---------- */
let flashTimer: ReturnType<typeof setTimeout> | null = null
function dispatchAdvice(): void {
  if (!props.live.best) return
  emit('dispatch')
  const node = el.shAdvBtnTxt
  if (!node) return
  const old = node.textContent ?? ''
  node.textContent = '已下发 ✓'
  if (flashTimer) clearTimeout(flashTimer)
  flashTimer = setTimeout(() => {
    el.shAdvBtnTxt && (el.shAdvBtnTxt.textContent = old)
  }, 1200)
}
function onAdvBtnKey(e: KeyboardEvent): void {
  if (e.key === 'Enter' || e.key === ' ') {
    e.preventDefault()
    dispatchAdvice()
  }
}

/* ---------- 生命周期 ---------- */
onMounted(() => {
  cacheEls()
  roll.value = Math.sin(props.live.tick * 0.42) * 0.32
  paint()
  raf = requestAnimationFrame(scrollLoop)
})
onBeforeUnmount(() => {
  cancelAnimationFrame(raf)
  if (flashTimer) clearTimeout(flashTimer)
})
watch(() => props.live, () => {
  roll.value = Math.sin(props.live.tick * 0.42) * 0.32
  paint()
})
watch(tipZone, (zone) => {
  tipTitle.value = zone ? ZONE_TITLE[zone] : '—'
  tipRows.value = zone ? buildTipRows(zone, props.live) : []
})
</script>

<style scoped lang="less">
@import '../styles/scene.less';
</style>
