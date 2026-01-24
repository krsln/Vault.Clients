# Vault Secret Enjeksiyon Modelleri (SDK / initContainer / Sidecar)

Bu doküman, **Vault secret’larının Pod içine nasıl alındığını** ve **uygulamanın (app) ne yapması gerektiğini** açıklar.

---

## 1️⃣ SDK (Application-level çözümleme)

### Beklenen Pod ENV Tanımı

```yaml
env:
  - name: POSTGRES_PASSWORD
    value: vault:postgres-password

  - name: POSTGRES_USER
    value: vault:postgres-user

  - name: NOT_EXIST_TEST
    value: vault:not-exist
```

### Pod İçinde Görünüm

```bash
/app# env | grep POSTGRES
POSTGRES_PASSWORD=vault:postgres-password
POSTGRES_USER=vault:postgres-user
```

➡️ **Secret’lar çözülmemiştir** (raw halde görünür).

---

### Uygulama Tarafı (Zorunlu)

Uygulama, Vault referanslarını runtime’da çözmek zorundadır.

```csharp
using Vault.SDK.Configuration;

builder.Configuration
       .AddEnvironmentVariables()
       .AddVault();
```

📌 `AddVault()`:

* `vault:*` formatını algılar
* Vault.API’ye gider
* Gerçek secret değerleriyle configuration’ı override eder

---

### Ne Zaman Kullanılır?

* Uygulama Vault’a **doğrudan bağımlıysa**
* Secret’lar **runtime’da** çekilecekse
* SDK entegrasyonu mümkünse

---

## 2️⃣ initContainer (Pod başlangıcında çözümleme)

### Beklenen initContainer ENV Tanımı

```yaml
vaultSecrets:
  POSTGRES_PASSWORD: postgres-password
  POSTGRES_USER: postgres-user
  NOT_EXIST_TEST: not-exist

# Template'te:
env:
  - name: VAULT_API
    value: "http://vault-api:80"
  - name: VAULT_SECRETS_LIST
    value: "{{ range $key, $path := .Values.vaultSecrets }}{{ $key }}={{ $path }},{{ end }}"
```

Örnek üretilen değer:

```
POSTGRES_PASSWORD=postgres-password,POSTGRES_USER=postgres-user
```

---

### Pod İçinde Görünüm

```bash
/app# env | grep POSTGRES
# görünmez (container env'inde yok)
```

```bash
/app# cat /vault/env/env-vars
export POSTGRES_PASSWORD=real_password
export POSTGRES_USER=real_user
```

➡️ Secret’lar **initContainer tarafından çözülmüş ve export edilmiştir**.
➡️ Runtime’da **aktif** durumdadır.

---

### Uygulama Tarafı

✅ **Hiçbir ek konfigürasyon gerekmez**

* Secret’lar OS environment olarak zaten set edilmiştir
* `builder.Configuration.AddEnvironmentVariables()` yeterlidir

---

### Ne Zaman Kullanılır?

* Uygulama **Vault bilmemeli** ise
* Legacy / 3rd-party app kullanılıyorsa
* Başlangıçta secret’lar hazır olmalıysa

---

## 3️⃣ Sidecar (Dosya tabanlı çözümleme)

### Beklenen Sidecar ENV Tanımı

```yaml
vaultSecrets:
  POSTGRES_PASSWORD: postgres-password
  POSTGRES_USER: postgres-user
  NOT_EXIST_TEST: not-exist

# Template'te:
env:
  - name: VAULT_API
    value: "http://vault-api:80"
  - name: VAULT_SECRETS_LIST
    value: "{{ range $key, $path := .Values.vaultSecrets }}{{ $key }}={{ $path }},{{ end }}"
  - name: VAULT_REFRESH_INTERVAL
    value: "300"   # 5 dk
```

---

### Pod İçinde Görünüm

```bash
/app# env | grep POSTGRES
# env içinde yok
```

```bash
/app# cat /vault/env/secrets.json
{
  "POSTGRES_PASSWORD": "real_password",
  "POSTGRES_USER": "real_user"
}
```

➡️ Secret’lar **dosya olarak yazılır**, ENV’e eklenmez.

---

### Uygulama Tarafı (Zorunlu)

```csharp
builder.Configuration
    .AddJsonFile(
        "/vault/env/secrets.json",
        optional: false,
        reloadOnChange: true
    );
```

📌 `reloadOnChange: true`

* Sidecar secret yenilerse
* Uygulama runtime’da günceller

---

### Ne Zaman Kullanılır?

* Secret rotasyonu gerekiyorsa
* ENV değişkeni kullanmak istenmiyorsa
* Uygulama config tabanlı çalışıyorsa

---

## 🔁 Kısa Karşılaştırma

| Yöntem            | Secret Nerede                 | App Müdahalesi    |
| ----------------- | ----------------------------- | ----------------- |
| **SDK**           | ENV (vault:* → runtime çözüm) | ✅ `AddVault()`    |
| **initContainer** | ENV (export edilmiş)          | ❌ Gerek yok       |
| **Sidecar**       | Dosya (`secrets.json`)        | ✅ `AddJsonFile()` |

---

## 🧠 Altın Kural

> **Secret’ı kim çözüyor?**

* App → **SDK**
* Pod → **initContainer**
* Yardımcı container → **Sidecar**

