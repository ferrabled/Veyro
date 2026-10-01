import sys,glob,os
from PIL import Image,ImageDraw
files=sorted(glob.glob('out/stills/t_*.png'),key=lambda f:float(f.split('t_')[1][:-4]))
if len(sys.argv)>1: files=[f for f in files if float(f.split('t_')[1][:-4]) in [float(x) for x in sys.argv[1:]]]
cols=2;w,h=960,540
for k in range(0,len(files),6):
    chunk=files[k:k+6];rows=(len(chunk)+cols-1)//cols
    sheet=Image.new('RGB',(cols*w,rows*h),'black');d=ImageDraw.Draw(sheet)
    for i,f in enumerate(chunk):
        im=Image.open(f).convert('RGB').resize((w,h),Image.LANCZOS);x,y=(i%cols)*w,(i//cols)*h;sheet.paste(im,(x,y))
        d.rectangle([x,y,x+90,y+26],fill='black');d.text((x+6,y+6),f.split('t_')[1][:-4]+'s',fill='yellow')
    sheet.save(f'out/sheet_{k//6}.png')
print('sheets',(len(files)+5)//6)
