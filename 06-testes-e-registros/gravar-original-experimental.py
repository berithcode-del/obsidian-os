from pathlib import Path
import hashlib,sys,time
image=Path(r'D:\R36S-sistema-novo\02-imagens-personalizadas\GA36-original-biblioteca-vazia-16GB-experimental.img')
expected=15485370368
block=1024*1024
if image.stat().st_size!=expected: raise SystemExit('ERRO: tamanho da imagem inesperado')
h=hashlib.sha256()
with image.open('rb') as src, open(r'\\.\PhysicalDrive2','r+b',buffering=0) as dst:
 total=0
 while total<expected:
  data=src.read(min(block,expected-total))
  if not data: raise IOError('Imagem interrompida')
  n=dst.write(data)
  if n!=len(data): raise IOError('Gravação incompleta')
  h.update(data); total+=n
  if total%(1024*1024*1024)==0: print('GRAVADOS',total//(1024*1024*1024),'GiB',flush=True)
 dst.flush()
print('GRAVAÇÃO TERMINOU. Iniciando leitura de verificação.',flush=True)
h2=hashlib.sha256()
with open(r'\\.\PhysicalDrive2','rb',buffering=0) as disk:
 total=0
 while total<expected:
  data=disk.read(min(block,expected-total))
  if not data: raise IOError('Leitura de verificação interrompida')
  h2.update(data);total+=len(data)
  if total%(1024*1024*1024)==0: print('VERIFICADOS',total//(1024*1024*1024),'GiB',flush=True)
print('SHA256 imagem:',h.hexdigest(),flush=True)
print('SHA256 cartão:',h2.hexdigest(),flush=True)
if h.digest()!=h2.digest():raise SystemExit('ERRO: imagem e cartão não conferem')
print('RESULTADO: VERIFICADO',flush=True)
