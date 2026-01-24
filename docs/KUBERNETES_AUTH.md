# Kubernetes (k8s) Kimlik Doğrulama Yöntemi Nasıl Çalışır?

Kubernetes (k8s) kimlik doğrulama yöntemi, modern bulut altyapılarında en güvenli ve tavsiye edilen yöntemdir. Bu doküman, bu sürecin nasıl işlediğini adım adım açıklamaktadır.

### Özet

Temel fikir şudur: **`Vault.API` bir pod'un kimliğine kendisi karar vermez; bu kararı ve doğrulamayı doğrudan Kubernetes'in kendisine yaptırır.** `Vault.API`, Kubernetes API sunucusuna "Bu token'a sahip olan pod gerçekten söylediği kişi mi?" diye sorar. Kubernetes "Evet, bu token geçerli ve sahibi de şu servis hesabıdır" cevabını verirse, `Vault.API` o pod'a güvenir.

---

### Analoji: Gece Kulübü Güvenliği

Bu süreci bir gece kulübüne girmeye benzetebiliriz:

*   **Siz (İstemci Pod):** Kulübe girmek isteyen kişisiniz.
*   **Kimliğiniz (K8s Service Account Token):** Yanınızda getirdiğiniz, devlet tarafından verilmiş resmi bir kimlik kartı (pasaport, ehliyet vb.).
*   **Kulüp Güvenliği (`Vault.API`):** Kimliğinizin gerçek olup olmadığını anlayacak uzmanlığa sahip değil. Sahte olup olmadığını bilemez.
*   **Polis (`Kubernetes API Server`):** Kimlikleri doğrulamak için resmi yetkiye ve sisteme sahip olan tek otorite.

**Akış:**

1.  Siz (Pod), kapıdaki güvenliğe (`Vault.API`) kimliğinizi (K8s Token) gösterirsiniz.
2.  Güvenlik (`Vault.API`), kimliğinizi alır ve telsizle en yakındaki polisi (`Kubernetes API Server`) arar. Polise "Elimde şu kimlik numarasına sahip bir kart var, sistemden kontrol eder misin?" der. Bu işlem, teknik olarak **`TokenReview`** API çağrısıdır.
3.  Polis (`Kubernetes API Server`), kendi sisteminde kimliğin geçerli olup olmadığını, süresinin dolup dolmadığını kontrol eder ve güvenliğe "Evet, o kimlik gerçek ve sahibi de Mehmet Yılmaz'dır" diye cevap verir.
4.  Bu onayı alan güvenlik (`Vault.API`), artık sizin kim olduğunuzdan emindir. Size kulüp içinde geçerli olan bir **bileklik (`Vault.API` Erişim Token'ı)** takar.
5.  Artık kulüp içinde (örneğin bara veya VIP alana giderken) tekrar tekrar kimliğinizi göstermenize gerek kalmaz. Sadece o gece o kulüpte geçerli olan bilekliği göstermeniz yeterlidir.

---

### Teknik Akış

1.  **Token'ın Alınması (Pod Tarafı):**
    *   Bir pod Kubernetes'te çalıştığında, Kubernetes otomatik olarak o pod'un içine `ServiceAccount`'a ait bir JWT (JSON Web Token) token'ını ` /var/run/secrets/kubernetes.io/serviceaccount/token` yoluna mount eder.
    *   `Vault.SDK` veya `Vault.UI` gibi bir istemci, kimlik doğrulaması yapacağı zaman bu dosyayı okur.

2.  **Kimlik Doğrulama İsteği (Pod'dan Vault.API'ye):**
    *   İstemci, okuduğu bu K8s token'ını bir `POST` isteği ile `Vault.API`'nin `/api/auth/k8s` endpoint'ine gönderir.

3.  **Token Doğrulama (Vault.API'den K8s API'ye):**
    *   `Vault.API`, istemciden gelen bu K8s token'ını alır.
    *   Kendisi bu token'ı çözmeye veya doğrulamaya çalışmaz. Bunun yerine, `TokenReview` adında bir nesne oluşturur ve bu nesneyi doğrudan **Kubernetes API sunucusuna** gönderir.
    *   Kubernetes API sunucusu, `TokenReview` isteğini alır, içindeki token'ı kendi anahtarlarıyla doğrular ve bir cevap döner. Bu cevapta token'ın geçerli olup olmadığı (`authenticated: true/false`) ve geçerliyse kime ait olduğu (örn: `serviceaccount:my-app` `namespace:production`) gibi bilgiler yer alır.

4.  **Yetkilendirme ve Vault Token'ı Oluşturma (Vault.API Tarafı):**
    *   `Vault.API`, Kubernetes'ten "doğrulandı" yanıtını alınca artık istemcinin kimliğinden emin olur.
    *   Kendi içindeki `policies.yaml` dosyasına bakar ve bu kimliğe (örneğin, `production` namespace'indeki `my-app` servis hesabına) hangi gizli verilere erişim izni verildiğini bulur.
    *   Bu izinleri içeren, kendi imzaladığı, kısa ömürlü yeni bir **`Vault.API` erişim token'ı** oluşturur.

5.  **Cevap (Vault.API'den Pod'a):**
    *   `Vault.API`, oluşturduğu bu yeni token'ı istemci pod'a cevap olarak döner.

Artık istemci pod, gizli verilere erişmek için yapacağı tüm sonraki isteklerde bu `Vault.API` token'ını `Authorization: Bearer <token>` başlığında kullanarak kendini doğrular. Bu yöntem, statik şifrelerin veya anahtarların kod veya yapılandırma dosyalarında saklanması ihtiyacını tamamen ortadan kaldırdığı için son derece güvenlidir.
