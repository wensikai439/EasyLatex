/* Deterministic product-flow illustrations, also used to render the README GIFs.
   This page never connects to a real room, compiler, API or clipboard. */
(() => {
  'use strict';
  const canvas = document.getElementById('demo-canvas'), ctx = canvas.getContext('2d');
  const C = {ink:'#242635',muted:'#858697',accent:'#6851cc',soft:'#eee9fc',line:'#e6e6ef',panel:'#f7f7fb',paper:'#fff',green:'#32866a',blue:'#438bb0'};
  const sans = '"Segoe UI", "Microsoft YaHei", sans-serif', mono = 'Consolas, "Microsoft YaHei", monospace';
  const sceneDuration = {blocks:9,images:11,collaboration:10,ai:12};
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
  function blocks(t){base('数模论文');if(t<3.6){code(['\\begin{document}','','\\end{document}']);caret(236,181,t);box(189,157,28,28,'white',7,C.line);icon('plus',193,161,20);if(t>=1.9)popup(t);const at=t<2.7?move(t,[434,230],[199,168],.45,1.55):move(t,[199,168],[303,238],2.7,3.3);cursor(...at,t>3.25&&t<3.6);}else{const lines=['\\begin{document}','','\\begin{abstract}',t<4.6?'在这里填写摘要。':'本文建立了数学模型，分析问题并给出',t<5.1?'':'求解方法。通过数据检验，验证模型的',t<5.3?'':'有效性与稳定性。','\\end{abstract}','','\\end{document}'];box(229,190,475,178,C.soft,4);code(lines);if(t<5.8)caret(430,283,t);else paper('abstract',ease((t-6.2)/.45));if(t>5.7&&t<6.4){cursor(...move(t,[432,280],[940,32],5.7,6),t>6&&t<6.3);pill('Ctrl+Enter',581,597,122);}else if(t>=6.4)pill('编译完成',580,597,122);}}
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
  function render(which,time){ctx.save();({blocks,images,collaboration,ai}[which])(time);ctx.restore();const items=stages[which];document.getElementById('stage').textContent=items.filter(([at])=>at<=time).at(-1)?.[1]??items[0][1];}
  function choose(which){scene=which;elapsed=0;previous=null;document.querySelectorAll('[data-scene]').forEach(b=>{const selected=b.dataset.scene===which;b.setAttribute('aria-selected',String(selected));b.tabIndex=selected?0:-1;});document.getElementById('demo-panel').setAttribute('aria-labelledby','tab-'+which);render(scene,0);}
  document.querySelectorAll('[data-scene]').forEach(b=>{b.addEventListener('click',()=>choose(b.dataset.scene));b.addEventListener('keydown',e=>{const all=[...document.querySelectorAll('[data-scene]')];let i=all.indexOf(b);if(e.key==='ArrowRight')i=(i+1)%all.length;else if(e.key==='ArrowLeft')i=(i+all.length-1)%all.length;else if(e.key==='Home')i=0;else if(e.key==='End')i=all.length-1;else return;e.preventDefault();all[i].focus();choose(all[i].dataset.scene);});});
  document.querySelectorAll('[data-play]').forEach(b=>b.addEventListener('click',()=>{choose(b.dataset.play);document.getElementById('demo').scrollIntoView({behavior:matchMedia('(prefers-reduced-motion: reduce)').matches?'instant':'smooth'});}));
  function updatePause(){document.getElementById('pause').textContent=paused?'播放':'暂停';document.getElementById('pause').setAttribute('aria-label',paused?'播放动画':'暂停动画');}
  document.getElementById('pause').addEventListener('click',()=>{paused=!paused;previous=null;updatePause();});document.getElementById('replay').addEventListener('click',()=>{elapsed=0;previous=null;render(scene,0);});
  document.addEventListener('visibilitychange',()=>{previous=null;});
  function tick(timestamp){if(automatic&&!paused&&!document.hidden){if(previous!==null)elapsed=(elapsed+(timestamp-previous)/1000)%sceneDuration[scene];render(scene,elapsed);}previous=timestamp;requestAnimationFrame(tick);}
  window.EasyLatexDemo={render:(which,time)=>{automatic=false;choose(which);render(which,time);},duration:sceneDuration};updatePause();render(scene,0);requestAnimationFrame(tick);
})();
