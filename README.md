# Linea

Agenda com frontend em Vue.js e backend em ASP.NET Core para organizar tarefas, anotações e lembretes. Abre sem login e guarda as anotações no navegador; a conta é opcional para acessar anotações em outros aparelhos.

## Como rodar

A API é necessária apenas para entrar em uma conta e salvar nela. Em um terminal, suba a API em modo de desenvolvimento:

```powershell
dotnet run --project Agenda.API\Agenda.API.csproj --urls http://localhost:5047
```

Em outro terminal, suba o frontend:

```powershell
cd Agenda.Web
npm install
npm run dev
```

Abra:

```text
http://127.0.0.1:5173/
```

O frontend local usa o proxy do Vite para chamar a API. Em produção, configure a variável `VITE_API_BASE_URL` com a URL do backend.

## Login de teste

Quando a API roda em modo de desenvolvimento, ela cria automaticamente este usuário de teste:

```text
E-mail: teste@agenda.local
Senha: Teste1
```

Esse login é apenas para teste local. Em produção, o usuário deve criar uma conta pelo próprio site.

## Armazenamento

Sem conta, **Salvar** guarda a anotacao no `localStorage` deste navegador, sem
consultar a API. Nao sao cookies. Limpar os dados do site, trocar de navegador
ou usar uma janela anonima pode deixar essas anotacoes indisponiveis.
Depois de a pagina carregar, o modo local nao depende do backend; o projeto
nao oferece instalacao PWA nem garante abrir a pagina sem internet.

O botao **Entrar** abre o acesso opcional. Na conta, **Salvar** usa o servidor.
As anotacoes locais e as da conta ficam separadas. **Levar anotacoes para minha
conta** transfere as locais apenas por escolha do usuario, removendo cada copia
local somente depois da confirmacao do servidor. Falhas mantem as restantes no
navegador. Repetir a transferencia nao duplica a mesma anotacao nessa conta.
Se a copia da conta ja tiver outro conteudo, a local e preservada para conferencia.
Um rascunho novo ainda nao salvo fica no modo local ate ser salvo.

Rascunhos sao guardados durante a edicao, separados por conta e pelo modo local.
Eles podem ser retomados ao trocar de anotacao ou recarregar, mas nao substituem
o botao **Salvar**. Sair da conta volta ao modo local, sem copiar dados da conta.

No celular, a lista e a folha sao vistas separadas. Os filtros e a busca permitem
encontrar pendentes, anotacoes de hoje, concluidas e textos nos detalhes.
A exclusao pede confirmacao e o som de trocar folhas vem desativado.

## Testes do frontend

```powershell
cd Agenda.Web
npm test
npm run build
```

Os testes usam o executor nativo do Node.js. Eles verificam armazenamento local,
isolamento dos rascunhos e erros/prazos das chamadas HTTP com respostas simuladas.

## Dados da agenda

Sem banco configurado, os usuários e as anotações ficam salvos localmente no arquivo:

```text
Agenda.API\App_Data\agenda-data.json
```

Esse arquivo fica no `.gitignore`, porque pode guardar dados pessoais de quem usa a agenda.

Em produção, configure `DATABASE_URL` com a connection string do Neon PostgreSQL. Quando essa variável existe, a API usa o banco PostgreSQL e cria as tabelas automaticamente.

## JWT

A API usa JWT assinado para proteger as rotas da agenda. Em desenvolvimento, a chave é temporária e criada quando a API liga. Em produção, configure uma variável de ambiente:

```powershell
$env:AGENDA_JWT_SECRET="troque-por-uma-chave-grande-e-segura-com-mais-de-32-caracteres"
```

## Variáveis de ambiente

Backend:

```env
ASPNETCORE_ENVIRONMENT=Production
AGENDA_JWT_SECRET=sua-chave-grande-e-segura
DATABASE_URL=sua-connection-string-do-neon
Cors__AllowedOrigins__0=https://sua-url-da-vercel.vercel.app
```

Frontend:

```env
VITE_API_BASE_URL=https://sua-api-do-render.onrender.com
```
