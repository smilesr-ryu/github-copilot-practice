"""비밀번호 보안 처리 모듈"""

import re
from passlib.context import CryptContext

# bcrypt 기반 비밀번호 해싱 설정
pwd_context = CryptContext(schemes=["bcrypt"], deprecated="auto")


def validate_password(password: str) -> bool:
    """
    비밀번호 검증 규칙:
    - 길이 >= 8
    - 숫자 1개 이상 포함
    - 특수문자 1개 이상 포함 (!@#$%^&* 등)
    
    Args:
        password: 검증할 비밀번호
        
    Returns:
        bool: 유효하면 True, 아니면 False
    """
    if not isinstance(password, str):
        return False
    
    # 길이 검사
    if len(password) < 8:
        return False
    
    # 숫자 포함 여부
    if not re.search(r"\d", password):
        return False
    
    # 특수문자 포함 여부
    if not re.search(r"[!@#$%^&*\-._]", password):
        return False
    
    return True


def hash_password(password: str) -> str:
    """
    비밀번호를 bcrypt로 해싱합니다.
    
    Args:
        password: 평문 비밀번호
        
    Returns:
        str: 해싱된 비밀번호
    """
    return pwd_context.hash(password)


def verify_password(password: str, hashed_password: str) -> bool:
    """
    평문 비밀번호와 해시된 비밀번호를 비교합니다.
    
    Args:
        password: 입력받은 평문 비밀번호
        hashed_password: 저장된 해시된 비밀번호
        
    Returns:
        bool: 일치하면 True, 아니면 False
    """
    return pwd_context.verify(password, hashed_password)
