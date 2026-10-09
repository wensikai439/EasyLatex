/* Deterministic product-flow illustrations, also used to render the README GIFs.
   This page never connects to a real room, compiler, API or clipboard. */
(() => {
  'use strict';
  const canvas = document.getElementById('demo-canvas'), ctx = canvas.getContext('2d');
  const C = {ink:'#242635',muted:'#858697',accent:'#6851cc',soft:'#eee9fc',line:'#e6e6ef',panel:'#f7f7fb',paper:'#fff',green:'#32866a',blue:'#438bb0'};
  const sans = '"Segoe UI", "Microsoft YaHei", sans-serif', mono = 'Consolas, "Microsoft YaHei", monospace';
  const sceneDuration = {blocks:12,formatting:11,images:11,collaboration:10,ai:12};
  let scene = 'blocks', elapsed = 0, previous = null, paused = matchMedia('(prefers-reduced-motion: reduce)').matches, automatic = true;
  const clamp = (x,a=0,b=1) => Math.max(a,Math.min(b,x));
  const ease = x => {x=clamp(x);return x*x*(3-2*x);};
  function box(x,y,w,h,fill,r=0,stroke){ctx.beginPath();ctx.roundRect(x,y,w,h,r);if(fill){ctx.fillStyle=fill;ctx.fill();}if(stroke){ctx.strokeStyle=stroke;ctx.lineWidth=1;ctx.stroke();}}
  function text(str,x,y,size=18,color=C.ink,font=sans,weight=400){ctx.fillStyle=color;ctx.font=`${weight} ${size}px ${font}`;ctx.textAlign='left';ctx.textBaseline='alphabetic';ctx.fillText(str,x,y);}
  function line(x,y,x2,y2,color=C.line,width=1){ctx.strokeStyle=color;ctx.lineWidth=width;ctx.beginPath();ctx.moveTo(x,y);ctx.lineTo(x2,y2);ctx.stroke();}
  function icon(kind,x,y,size=18,color=C.accent){ctx.save();ctx.translate(x,y);ctx.scale(size/20,size/20);ctx.strokeStyle=color;ctx.fillStyle=color;ctx.lineWidth=1.5;ctx.lineCap='round';ctx.lineJoin='round';ctx.beginPath();
    if(kind==='plus'){ctx.moveTo(4,10);ctx.lineTo(16,10);ctx.moveTo(10,4);ctx.lineTo(10,16);}
    if(kind==='summary'){[5,10,15].forEach((v,i)=>{ctx.moveTo(3,v);ctx.lineTo(i===2?12:17,v);});}
    if(kind==='section'){ctx.moveTo(4,3);ctx.lineTo(4,17);ctx.moveTo(16,3);ctx.lineTo(16,17);ctx.moveTo(4,10);ctx.lineTo(16,10);}
    if(kind==='formula'){ctx.moveTo(16,3);ctx.lineTo(5,3);ctx.lineTo(11,10);ctx.lineTo(5,17);ctx.lineTo(16,17);}
    if(kind==='table'){[4,10,16].forEach(v=>{ctx.moveTo(2,v);ctx.lineTo(18,v);});}
    if(kind==='image'){ctx.rect(2,3,16,14);ctx.moveTo(3,16);ctx.lineTo(9,9);ctx.lineTo(13,13);ctx.lineTo(15,10);ctx.lineTo(18,14);}
    if(kind==='people'){ctx.arc(7,6,3,0,Math.PI*2);ctx.moveTo(1,17);ctx.bezierCurveTo(1,10,13,10,13,17);ctx.moveTo(14,3);ctx.bezierCurveTo(20,3,20,9,15,9);ctx.moveTo(15,12);ctx.bezierCurveTo(19,12,20,14,20,17);}
    ctx.stroke();ctx.restore();
  }
  function cursor(x,y,click=false,color=C.ink){ctx.save();ctx.translate(x,y);if(click){ctx.globalAlpha=.25;ctx.beginPath();ctx.arc(3,5,17,0,Math.PI*2);ctx.fillStyle=C.accent;ctx.fill();ctx.globalAlpha=1;}ctx.beginPath();ctx.moveTo(0,0);ctx.lineTo(0,21);ctx.lineTo(6,16);ctx.lineTo(10,24);ctx.lineTo(14,22);ctx.lineTo(10,14);ctx.lineTo(18,13);ctx.closePath();ctx.fillStyle=color;ctx.fill();ctx.strokeStyle='white';ctx.lineWidth=2;ctx.stroke();ctx.restore();}
  function move(t,a,b,start,end){const p=ease((t-start)/(end-start));return [a[0]+(b[0]-a[0])*p,a[1]+(b[1]-a[1])*p];}
  function base(title='数模论文',collab=false){ctx.clearRect(0,0,1280,700);box(0,0,1280,700,C.panel);box(0,0,1280,66,'white');line(0,66,1280,66);box(22,16,34,34,C.accent,9);text('E',31,41,25,'white','Georgia',700);text('EasyLatex',69,42,23,C.ink,sans,700);text('文件',220,41,16);text('编辑',274,41,16);text('视图',328,41,16);box(887,15,149,36,C.accent,8);text('编译 Ctrl+Enter',900,39,16,'white',sans,600);icon('people',1062,25,21,C.ink);text('协作',1090,40,16);text('✦ AI',1176,40,17);box(0,67,174,601,C.panel);line(174,67,174,668);text('大纲',23,106,15,C.muted,sans,600);text('摘要',32,152,16);text('问题分析',32,190,16);text('模型建立',32,228,16);text('结果与检验',32,266,16);text('项目文件',23,566,15,C.muted,sans,600);text('main.tex',32,607,15);box(175,67,576,601,'white');line(751,67,751,668);box(752,67,528,44,'white');text('PDF 预览',778,96,14,C.muted);text('适合宽度',1169,96,14);box(0,668,1280,32,'white');line(0,668,1280,668);text(title,20,690,13,C.muted);if(collab){text('房间已连接 · 3 人',1063,690,13,C.accent);}}
  function code(lines,y=147,highlight=-1,step=34){lines.forEach((s,i)=>{if(i===highlight)box(229,y-23+i*step,475,31,C.soft,4);const m=s.match(/^(\s*)(\\[a-zA-Z]+)(.*)$/);if(m){text(m[1]+m[2],236,y+i*step,19,C.accent,mono);text(m[3],236+ctx.measureText(m[1]+m[2]).width,y+i*step,19,C.ink,mono);}else text(s,236,y+i*step,19,C.ink,mono);});}
  function paper(kind='abstract',alpha=1){ctx.save();ctx.globalAlpha=alpha;box(797,135,438,504,'white',1,'#ecebf1');text('数学建模论文',903,190,25,C.ink,'Georgia, "Microsoft YaHei", serif');text('团队的每个想法，都在这里汇合。',856,224,15,C.muted);line(844,250,1184,250);if(kind==='abstract'){text('摘 要',972,292,21,C.ink,sans,600);text('本文建立了数学模型，分析问题并给出',847,332,15);text('求解方法。通过数据检验，验证模型的',847,360,15);text('有效性与稳定性。',847,388,15);text('1  问题分析',847,450,20,C.ink,sans,600);}if(kind==='image'){text('2  结果与检验',846,288,20,C.ink,sans,600);chart(852,317,322,198);text('图 1：模型预测与实际数据',893,553,15);}if(kind==='collaboration'){text('1  问题分析',847,290,20,C.ink,sans,600);text('我们从数据与假设出发，建立模型。',847,332,15);text('2  模型建立',847,404,20,C.ink,sans,600);text('目标函数兼顾预测精度与稳定性。',847,446,15);}if(kind==='ai'){text('2  模型建立',847,290,20,C.ink,sans,600);text('模型的目标函数为',847,334,15);text('f(x) = x² + 1',929,397,26,C.ink,'Georgia,serif');text('据此求解模型，并对结果进行检验。',847,466,15);}ctx.restore();}
  function chart(x,y,w,h){box(x,y,w,h,'#f8f7fc',6);line(x+32,y+h-28,x+w-20,y+h-28,'#b6b4c2');line(x+32,y+20,x+32,y+h-28,'#b6b4c2');const p=[.8,.64,.69,.48,.36,.42,.21];ctx.beginPath();ctx.strokeStyle=C.accent;ctx.lineWidth=3;p.forEach((v,i)=>{const px=x+40+i*(w-65)/6,py=y+20+v*(h-60);i?ctx.lineTo(px,py):ctx.moveTo(px,py);});ctx.stroke();p.forEach((v,i)=>{box(x+36+i*(w-65)/6,y+16+v*(h-60),8,8,C.accent,4);});}
  function caret(x,y,t,color=C.accent){if(Math.floor(t*2)%2===0)line(x,y-20,x,y+5,color,2);}
  function pill(str,x,y,width=140){box(x,y,width,32,C.soft,6);text(str,x+12,y+22,14,C.accent,sans,500);}
  function popup(t){box(206,211,303,227,'white',12,C.line);const entries=[['summary','摘要'],['section','章节'],['formula','公式'],['table','三线表'],['image','图片']];entries.forEach(([k,label],i)=>{if(i===0&&t>2.7)box(213,219+i*42,289,37,C.soft,7);icon(k,230,228+i*42,19);text(label,263,245+i*42,17);});}
  // Close-up views keep source text and field hints readable in the README.
  function focused(){
    ctx.clearRect(0,0,1280,700);box(0,0,1280,700,'white');
    box(20,17,34,34,C.accent,9);text('E',29,42,25,'white','Georgia',700);text('EasyLatex',68,43,23,C.ink,sans,700);
    text('文件',228,42,17);text('编辑',287,42,17);text('视图',346,42,17);
    box(837,15,152,39,C.accent,8);text('编译 Ctrl+Enter',850,41,17,'white',sans,600);
    icon('people',1017,24,23,C.ink);text('协作',1048,41,17);text('✦ AI',1166,41,18);
    line(0,69,1280,69);box(800,70,480,630,C.panel);line(800,70,800,700);
    box(801,70,479,49,'white');text('PDF · 1 页',824,102,17,C.muted);
    text('适合宽度',1029,102,16,C.muted);box(1125,80,133,31,'white',6,C.line);text('导出 PDF',1150,102,16);
    box(831,143,419,515,'white',1,C.line);
  }
  function focusedCode(lines,y=171){
    ctx.save();ctx.beginPath();ctx.rect(92,92,700,575);ctx.clip();
    lines.forEach((s,i)=>{const m=s.match(/^(\s*)(\\[a-zA-Z]+)(.*)$/);if(m){text(m[1]+m[2],108,y+i*46,24,C.accent,mono);text(m[3],108+ctx.measureText(m[1]+m[2]).width,y+i*46,24,C.ink,mono);}else text(s,108,y+i*46,24,C.ink,mono);});ctx.restore();
  }
  function field(value,x,y,label,active=true){
    ctx.font=`400 24px ${mono}`;const w=ctx.measureText(value).width;
    box(x-2,y-27,w+5,35,active?'#d8e7ff':'#edf4ff',4,active?'#7ba7f2':'#c4d6f7');
    text(value,x,y,24,'#2559aa',mono);
    if(active){box(x-1,y-63,Math.max(60,label.length*19+16),28,'#f3f7ff',5,'#cadbfa');text(label,x+8,y-43,18,'#3867b0');}
  }
  function blankEntry(y){box(38,y-24,35,35,'white',8,C.line);icon('plus',44,y-18,23,C.accent);}
  function focusedMenu(y,selected){
    box(37,y,265,248,'white',10,C.line);
    [['summary','摘要'],['section','章节'],['formula','公式'],['table','三线表'],['image','图片']].forEach(([k,label],i)=>{
      if(i===selected)box(44,y+8+i*46,251,42,C.soft,6);
      icon(k,57,y+18+i*46,23);text(label,97,y+37+i*46,21);
    });
  }
  function blocks(t){
    focused();const inserted=t>=3.15, typed=t>=5.4, math=t>=7.2;
    focusedCode(['\\begin{document}','','','',...(inserted?[]:['\\end{document}'])]);
    if(!inserted){
      blankEntry(263);caret(109,263,t);if(t>=1.55)focusedMenu(282,t>=2.5?1:-1);
      cursor(...(t<2.5?move(t,[389,279],[52,247],.4,1.2):move(t,[52,247],[159,351],2.5,2.95)),t>=3&&t<3.15);
    }else{
      // Generated source remains source; the blue overlays are editor hints.
      if(t<9.3)box(100,238,671,83,C.soft,5);
      focusedCode(['\\section{'+(typed?'模型建立与求解':'Section title')+'}\\label{sec:topic}'],263);
      if(t<6.5){field(typed?'模型建立与求解':'Section title',238,263,'标题');box(38,239,35,35,'white',8,C.line);text('H₁',43,263,20,C.accent);}
      focusedCode([math?'\\[':'',math?'  \\frac{x}{y}':'',math?'\\]':'','','\\end{document}'],401);
      if(t>=6.5&&t<7.2){blankEntry(401);focusedMenu(416,2);cursor(...move(t,[306,263],[162,545],6.5,7),t>=7.05);}
      if(math&&t<9.3){
        box(100,374,671,129,C.soft,5);focusedCode(['\\[','  \\frac{x}{y}','\\]'],401);
        box(38,377,35,35,'white',8,C.line);icon('formula',44,383,23,C.accent);
        // x=252 / y=296 follows the measured monospace command advance.
        ctx.font=`400 24px ${mono}`;const x=108+ctx.measureText('  \\frac{').width;
        const second=x+ctx.measureText('x}{').width;
        field('x',x,447,'分子',t<8.25);field('y',second,447,'分母',t>=8.25);
        cursor(t<8.25?x+10:x+54,461);
      }else if(t<6.5)cursor(typed?413:337,279);
      if(t>=9.3){cursor(...move(t,[320,450],[356,534],9.3,9.8));caret(356,539,t);blankEntry(539);}
    }
    if(typed)text('模型建立与求解',900,252,24,C.ink,sans,600);
    if(t>=9.3){text('x',1040,375,28,C.ink,'Georgia',400);line(1027,386,1068,386,C.ink,1.5);text('y',1040,416,28,C.ink,'Georgia',400);}
  }
  function formatting(t){
    focused();const enlarged=t>=5.2;
    const source=enlarged?['{\\large','模型通过数据验证，','并分析关键参数的影响。','\\par}']:['模型通过数据验证，','并分析关键参数的影响。'];
    focusedCode(['\\section{结果与验证}'],174);
    if(t>=1.3&&t<5.2)box(100,261,672,93,C.soft,5);
    if(enlarged&&t<8.7)box(100,261,672,183,C.soft,5);
    focusedCode(source,296);focusedCode(['\\section{敏感性分析}'],572);
    box(37,272,36,35,'white',8,C.line);text('T',47,298,22,C.accent);
    if(t>=1.6&&t<5.2){
      box(37,324,270,136,'white',10,C.line);text('正文',53,352,17,C.muted);
      ['T','H₁','H₂','H₃'].forEach((v,i)=>text(v,58+i*58,393,21,i===0?C.accent:C.ink));
      ['B','I','A−','A+'].forEach((v,i)=>{if(i===3&&t>=4.1)box(218,410,67,37,C.soft,6);text(v,58+i*58,436,21,C.ink,sans,i===0?700:400);});
    }
    if(t<5.2)cursor(...(t<3.5?move(t,[380,328],[52,285],.4,1.25):move(t,[52,285],[250,423],3.5,4.35)),t>4.65&&t<5.2);
    else cursor(...move(t,[250,423],[473,580],7.8,8.7));
    text('结果与验证',928,233,24,C.ink,sans,600);
    const size=19+5*ease((t-5.45)/.65);text('模型通过数据验证，',868,293,size);text('并分析关键参数的影响。',868,334,size);
    if(t>=8.7)caret(474,585,t);
  }
  function images(t){base('粘贴图片');code(['\\section{结果与检验}','模型预测结果如下。','','','\\end{document}']);if(t<1.15){pill('Ctrl+V 粘贴图片',510,528,187);cursor(518,260);}else if(t<5.25){box(175,67,576,49,C.panel);box(198,76,205,31,'white',4,C.line);text(t<2.7?'figure':'预测结果',211,98,16);text('.png',412,98,14,C.muted);text('点击淡色位置',484,98,13,C.muted);box(230,201,475,18,C.soft,4);box(230,237,475,18,C.soft,4);icon('plus',675,201,17);icon('plus',675,237,17);chart(384,351,219,132);pill('剪贴板中的图片',413,506,168);const at=t<3.6?move(t,[518,260],[288,89],1.45,2.15):move(t,[288,89],[487,243],3.7,4.4);cursor(...at,t>4.75);}else{box(229,195,475,242,C.soft,4);code(['\\section{结果与检验}','模型预测结果如下。','','\\begin{figure}[htbp]','  \\centering','  \\includegraphics[width=.8\\linewidth]', '    {figures/预测结果.png}','  \\caption{模型预测与实际数据}','\\end{figure}'],147,-1,32);pill('已保存到 figures/',441,510,200);if(t>6.8){cursor(...move(t,[487,243],[940,32],6.8,7.3),t>7.3&&t<7.6);paper('image',ease((t-7.7)/.45));}}}
  function remoteCaret(x,y,name,color){line(x,y-22,x,y+6,color,2);box(x,y-46,58,23,color,4);text(name,x+8,y-30,12,'white',sans,500);}
  function collaboration(t){base('数模论文',t>=4);code(['\\section{问题分析}','我们从数据与假设出发，建立模型。','','\\section{模型建立}',t>=5.7?'目标函数兼顾预测精度与稳定性。':'','']);if(t<4){box(515,148,423,337,'white',14,C.line);icon('people',540,173,22);text(t<1.8?'开启房间':'加入申请',575,192,21,C.ink,sans,600);text('数模小组',541,237,18);if(t<1.8){text('附近的同伴可以申请加入',541,278,16,C.muted);box(769,414,138,42,C.accent,7);text('开启房间',802,441,16,'white');cursor(...move(t,[950,42],[823,431],.35,1.1),t>1.35);}else{text('小林申请加入项目',541,281,17);text('你允许后，才会发送文稿',541,316,14,C.muted);box(769,414,138,42,C.accent,7);text('允许加入',801,441,16,'white');cursor(...move(t,[823,431],[820,431],2,2.6),t>3.25);}}else{remoteCaret(t<5.7?268:580,283,'小林',C.green);remoteCaret(355,181,'小周',C.blue);pill('三个人，同一篇论文',480,508,223);if(t>6.8)paper('collaboration',ease((t-7.3)/.5));cursor(535,387);}}

  function ai(t){
    base('外部 AI · MCP');const fixed=t>=6.7;
    code(['\\section{模型建立}','模型的目标函数为','','\\begin{equation}',fixed?'  f(x) = x^{2} + 1':'  f(x) = x^{2 + 1','\\end{equation}']);
    if(t<2.2){
      box(527,146,452,355,'white',12,C.line);text('本机 AI 工具接入',552,187,22,C.ink,sans,600);
      box(551,211,18,18,t>.5?C.accent:'white',4,C.line);if(t>.5)text('✓',554,225,14,'white');
      text('允许本机 AI 工具读写文档',585,228,17);
      box(551,260,125,37,C.soft,6);text('Codex',582,285,16,C.accent);
      box(686,260,182,37,C.panel,6);text('Claude Code',715,285,16);
      text('按当前应用位置生成接入配置',552,341,15,C.muted);
      box(769,426,180,43,C.accent,7);text('复制接入配置',807,454,16,'white');
      cursor(...move(t,[660,354],[840,442],.8,1.7),t>1.9);
    }else if(t<9.2){
      box(778,128,472,510,'#25252f',11);
      text('Codex / Claude Code',800,167,19,'#f5f3fb',sans,600);line(799,185,1228,185,'#41414e');
      if(t<4.2){
        text(t<3.2?'codex mcp add easylatex --':'claude mcp add --transport stdio',800,225,14,'#b9adef',mono);
        text(t<3.2?'  \"D:/EasyLatex/EasyLatex.exe\" --mcp':'  easylatex -- \"EasyLatex.exe\" --mcp',800,253,13,'#e5e2ee',mono);
        text('接入 EasyLatex',800,313,17,'#8cd2b2');cursor(960,354);
      }else{
        text('› 修复当前公式的语法，再编译。',800,225,16,'#f5f3fb');
        const calls=[['get_document','读取当前源码',4.4],['get_diagnostics','读取编译错误',5.1],['apply_edits','补齐右花括号',6.1],['compile','调用 EasyLatex 编译',7.2],['get_diagnostics','确认编译通过',8.2]];
        calls.forEach(([name,label,at],i)=>{if(t>=at){text('✓ '+name,800,292+i*55,16,'#b9adef',mono);text(label,821,315+i*55,13,'#b5b3c1');}});
        if(fixed)box(229,258,475,35,C.soft,5);
        code([fixed?'  f(x) = x^{2} + 1':'  f(x) = x^{2 + 1'],283);
        cursor(967,584);
      }
    }else{
      box(229,258,475,35,C.soft,5);code(['  f(x) = x^{2} + 1'],283);paper('ai');
      pill('AI 已调用编译，PDF 已更新',377,513,329);
      text('Codex / Claude Code  ·  MCP  ·  EasyLatex',245,582,17,C.muted);
    }
  }
  const stages={blocks:[[0,'把光标移到行边的＋号。'],[1.9,'悬停展开常用模块，点击「摘要」。'],[3.6,'代码自动生成，直接填写内容。'],[5.8,'Ctrl+Enter 保存并编译，右侧查看 PDF。']],images:[[0,'在编辑器直接粘贴图片。'],[1.15,'正文标出合适的位置，文件名可直接修改。'],[3.7,'点击一个淡色位置。'],[5.25,'图片保存到 figures/，代码自动生成并高亮。'],[7.7,'填写图题，编译后查看排版。']],collaboration:[[0,'房主开启局域网房间。'],[1.8,'同伴从附近房间申请加入，房主确认。'],[4,'一起编辑，看到彼此的光标。'],[6.8,'章节、图片与参考文献留在同一个项目里。']],ai:[[0,'遇到编译错误，打开 AI 助手。'],[1.5,'按选中文段或编译错误，请 AI 提出修改。'],[3.2,'审阅修改建议。'],[7.2,'应用后重新编译；修改仍可撤销。']]};
  stages.formatting=[[0,'正文旁显示 T，一个入口对应一个功能块。'],[1.3,'悬停标亮整块，自动识别为正文。'],[3.5,'点击 A+，放大这一块的字号。'],[5.2,'生成真实 LaTeX 格式，PDF 随之更新。']];
  stages.blocks=[[0,'空行旁是＋号，悬停展开常用模块。'],[3.15,'点击章节，蓝色标出可改的标题。'],[5.4,'直接填写标题。'],[7.2,'公式的可填处分别提示分子、分母。'],[9.3,'离开代码块，蓝色提示消失。']];
  function render(which,time){ctx.save();({blocks,formatting,images,collaboration,ai}[which])(time);ctx.restore();const items=stages[which];document.getElementById('stage').textContent=items.filter(([at])=>at<=time).at(-1)?.[1]??items[0][1];}
  function choose(which){scene=which;elapsed=0;previous=null;document.querySelectorAll('[data-scene]').forEach(b=>{const selected=b.dataset.scene===which;b.setAttribute('aria-selected',String(selected));b.tabIndex=selected?0:-1;});document.getElementById('demo-panel').setAttribute('aria-labelledby','tab-'+which);render(scene,0);}
  document.querySelectorAll('[data-scene]').forEach(b=>{b.addEventListener('click',()=>choose(b.dataset.scene));b.addEventListener('keydown',e=>{const all=[...document.querySelectorAll('[data-scene]')];let i=all.indexOf(b);if(e.key==='ArrowRight')i=(i+1)%all.length;else if(e.key==='ArrowLeft')i=(i+all.length-1)%all.length;else if(e.key==='Home')i=0;else if(e.key==='End')i=all.length-1;else return;e.preventDefault();all[i].focus();choose(all[i].dataset.scene);});});
  document.querySelectorAll('[data-play]').forEach(b=>b.addEventListener('click',()=>{choose(b.dataset.play);document.getElementById('demo').scrollIntoView({behavior:matchMedia('(prefers-reduced-motion: reduce)').matches?'instant':'smooth'});}));
  function updatePause(){document.getElementById('pause').textContent=paused?'播放':'暂停';document.getElementById('pause').setAttribute('aria-label',paused?'播放动画':'暂停动画');}
  document.getElementById('pause').addEventListener('click',()=>{paused=!paused;previous=null;updatePause();});document.getElementById('replay').addEventListener('click',()=>{elapsed=0;previous=null;render(scene,0);});
  document.addEventListener('visibilitychange',()=>{previous=null;});
  function tick(timestamp){if(automatic&&!paused&&!document.hidden){if(previous!==null)elapsed=(elapsed+(timestamp-previous)/1000)%sceneDuration[scene];render(scene,elapsed);}previous=timestamp;requestAnimationFrame(tick);}
  window.EasyLatexDemo={render:(which,time)=>{automatic=false;choose(which);render(which,time);},duration:sceneDuration};updatePause();render(scene,0);requestAnimationFrame(tick);
})();
