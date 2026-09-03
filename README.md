# Linea

Agenda online com backend em ASP.NET Core e frontend em Vue.js para organizar tarefas, anotações e lembretes com login, marcação de concluídas e indicação de atrasos.

## Como rodar

Em um terminal, suba a API em modo de desenvolvimento:

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
Senha: 123456
```

Esse login é apenas para teste local. Em produção, o usuário deve criar uma conta pelo próprio site.

## Armazenamento

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
